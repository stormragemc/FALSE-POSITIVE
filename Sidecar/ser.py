"""Structured HuBERT speech-affect observations.

The default checkpoint is a four-class IEMOCAP emotion classifier. Its output
is a fallible acoustic impression, never evidence of deception, guilt, intent,
or truth. ``prosody.py`` owns reliability and session-relative interpretation.
"""

from dataclasses import dataclass
import math
import time

import numpy as np
import torch
from transformers import AutoFeatureExtractor, AutoModelForAudioClassification

import config


_LABEL_DISPLAY_NAMES = {
    "neu": "neutral",
    "neutral": "neutral",
    "hap": "happy",
    "happy": "happy",
    "ang": "angry",
    "angry": "angry",
    "sad": "sad",
}
SUPPORTED_EMOTION_LABELS = frozenset({"neutral", "happy", "angry", "sad"})

_feature_extractor = None
_model = None
_device = "unavailable"

# Mid-band of a comfortable speaking level, ~-26 dBFS.
_TARGET_RMS = 0.05
# Silence trimming, applied before normalization so the RMS target is computed
# from speech rather than from speech diluted by dead air.
_TRIM_FRAME = 320          # 20ms at 16kHz
_TRIM_HOP = 160            # 10ms
_TRIM_MARGIN_SAMPLES = 1600  # keep 100ms either side; onsets carry affect
_TRIM_REL_FLOOR = 0.15     # share of the clip's own p95 frame energy
_TRIM_ABS_FLOOR = 1e-4
# Below this the clip is effectively silence; scaling it only amplifies noise
# into something that looks like speech. features_classical.py raises
# near_silence for these anyway, and prosody.py drops the turn as unreliable.
_MIN_NORMALIZE_RMS = 1e-4


@dataclass(frozen=True)
class HubertObservation:
    label: str
    confidence: float
    probabilities: dict[str, float]
    normalized_entropy: float
    top_two_margin: float
    embedding: np.ndarray
    frame_instability: float
    elapsed_ms: int
    model_id: str


def _resolve_device(requested: str) -> str:
    requested = requested.strip().lower()
    if requested != "auto":
        return requested
    if torch.cuda.is_available():
        return "cuda"
    # HuBERT sequence classification has terminated the process inside MPS
    # warm-up on tested Apple hardware instead of raising a catchable error.
    # CPU is the safe automatic fallback; advanced users may still opt into
    # HUBERT_DEVICE=mps explicitly to retest newer torch/transformers builds.
    return "cpu"


def load():
    global _feature_extractor, _model, _device
    if _model is None:
        target_device = _resolve_device(config.HUBERT_DEVICE)
        try:
            source_note = (
                "baked local snapshot"
                if config.HUBERT_LOCAL_FILES_ONLY
                else "pinned snapshot; downloads on first local run"
            )
            print(
                f"[Sidecar] Loading affect model '{config.HUBERT_MODEL_ID}' "
                f"on {target_device} ({source_note})..."
            )
            shared_load_options = {
                "revision": config.HUBERT_MODEL_REVISION,
                "local_files_only": config.HUBERT_LOCAL_FILES_ONLY,
            }
            extractor = AutoFeatureExtractor.from_pretrained(
                config.HUBERT_MODEL_ID,
                **shared_load_options,
            )
            model = AutoModelForAudioClassification.from_pretrained(
                config.HUBERT_MODEL_ID,
                **shared_load_options,
                use_safetensors=True,
            )
            model.to(target_device)
            model.eval()
            _feature_extractor = extractor
            _model = model
            _device = target_device
            # Validate the richer forward path now, while startup status is observable.
            warmup = _analyze_impl(np.zeros(16000, dtype=np.float32))
            observed_labels = set(warmup.probabilities)
            if observed_labels != SUPPORTED_EMOTION_LABELS:
                raise RuntimeError(
                    "HuBERT checkpoint has incompatible emotion labels: "
                    f"expected {sorted(SUPPORTED_EMOTION_LABELS)}, got {sorted(observed_labels)}"
                )
        except Exception:
            _feature_extractor = None
            _model = None
            _device = "unavailable"
            raise
        print("[Sidecar] Affect model ready.")
    return _feature_extractor, _model


def is_loaded() -> bool:
    return _model is not None


def device() -> str:
    return _device if is_loaded() else "unavailable"


def _display_label(raw_label: str) -> str:
    return _LABEL_DISPLAY_NAMES.get(str(raw_label).lower(), str(raw_label).lower())


def _trim_silence(audio: np.ndarray) -> np.ndarray:
    """Drop leading and trailing dead air before the encoder sees the clip.

    The classifier mean-pools over every frame, so dead air is not neutral
    padding — it is a vote. Measured 6 Aug on one clip at a fixed level, adding
    2s of room tone to each end moved `neutral` from 0.078 to 0.005 and `happy`
    from 0.063 to 0.123, and diluted frame_instability from 0.169 to 0.119.
    A hesitant witness produces exactly that shape of clip, which is the
    opposite of what the affect channel should be rewarding.

    Only leading and trailing silence goes. Internal pauses stay, because they
    are affect, and features_classical.py measures them on the untrimmed audio
    regardless — this function does not touch what the pacing channel sees.
    """
    if audio.size < _TRIM_FRAME * 2:
        return audio
    frames = np.lib.stride_tricks.sliding_window_view(audio, _TRIM_FRAME)[::_TRIM_HOP]
    energies = np.sqrt(np.mean(np.square(frames, dtype=np.float64), axis=1))
    if energies.size == 0:
        return audio
    floor = max(_TRIM_ABS_FLOOR, _TRIM_REL_FLOOR * float(np.percentile(energies, 95)))
    voiced = np.flatnonzero(energies >= floor)
    if voiced.size == 0:
        return audio  # nothing crossed the floor; let the quality flags handle it
    # The margins alone guarantee a usable remnant: even a single voiced frame
    # yields margin + frame + margin, and edge clamping only widens that.
    start = max(0, int(voiced[0]) * _TRIM_HOP - _TRIM_MARGIN_SAMPLES)
    end = min(audio.size, int(voiced[-1]) * _TRIM_HOP + _TRIM_FRAME + _TRIM_MARGIN_SAMPLES)
    return audio[start:end]


def _normalize_level(audio: np.ndarray) -> np.ndarray:
    """Scale to a fixed RMS so mic level stops leaking into the prediction.

    This checkpoint ships preprocessor_config.json with do_normalize=false, so
    raw waveform amplitude reaches the encoder unscaled — unusual, every
    comparable SER checkpoint sets it true. Measured 6 Aug, one identical clip
    replayed across a -34..-10 dBFS ladder moved `angry` by 0.104 unnormalized
    and 0.013 normalized. Loudness there is a property of the room, the mic and
    how close the player is sitting, not of how the witness feels.

    The residual after normalizing is clipping, which is destructive and cannot
    be undone here; features_classical.py flags it and prosody.py derates the
    turn. True level is still measured there for the reliability flags, so
    nothing downstream loses access to it.
    """
    rms = float(np.sqrt(np.mean(np.square(audio, dtype=np.float64))))
    if rms < _MIN_NORMALIZE_RMS:
        return audio
    return np.clip(audio * (_TARGET_RMS / rms), -1.0, 1.0).astype(np.float32)


def _bounded_window(audio: np.ndarray) -> np.ndarray:
    """The head of a long answer is all HuBERT reads.

    Transformer cost grows with input length and this is the slowest stage of a
    turn, so the emotion label is formed from the first HUBERT_MAX_SECONDS.
    Enforced here rather than by the caller so the bound holds for every entry
    point, and split out of _analyze_impl so it is testable without a loaded
    model. The classical features deliberately do not share this window — they
    read the whole utterance; see app.py:_flag_hubert_window.
    """
    maximum_samples = int(round(config.HUBERT_MAX_SECONDS * 16000))
    if audio.size > maximum_samples:
        return audio[:maximum_samples]
    return audio


def _analyze_impl(audio_f32_16k: np.ndarray) -> HubertObservation:
    audio = np.asarray(audio_f32_16k, dtype=np.float32).reshape(-1)
    if audio.size == 0:
        raise ValueError("HuBERT requires non-empty audio")
    if not np.all(np.isfinite(audio)):
        raise ValueError("HuBERT audio contains non-finite samples")

    audio = _normalize_level(_trim_silence(_bounded_window(audio)))

    t0 = time.perf_counter()
    inputs = _feature_extractor(audio, sampling_rate=16000, return_tensors="pt")
    inputs = {name: value.to(_device) for name, value in inputs.items()}
    with torch.inference_mode():
        outputs = _model(**inputs, output_hidden_states=True, return_dict=True)

    probs_tensor = torch.softmax(outputs.logits, dim=-1)[0].detach().float().cpu()
    values = probs_tensor.numpy().astype(np.float64)
    if values.size == 0 or not np.all(np.isfinite(values)):
        raise RuntimeError("HuBERT returned invalid probabilities")

    probabilities: dict[str, float] = {}
    for index, value in enumerate(values):
        raw = _model.config.id2label.get(index, str(index))
        label = _display_label(raw)
        probabilities[label] = probabilities.get(label, 0.0) + float(value)

    ranked = sorted(probabilities.items(), key=lambda item: item[1], reverse=True)
    label, confidence = ranked[0]
    runner_up = ranked[1][1] if len(ranked) > 1 else 0.0
    distribution = np.asarray(list(probabilities.values()), dtype=np.float64)
    entropy = -float(np.sum(distribution * np.log(np.maximum(distribution, 1e-12))))
    normalized_entropy = entropy / math.log(distribution.size) if distribution.size > 1 else 0.0

    hidden_states = outputs.hidden_states
    if not hidden_states:
        raise RuntimeError("HuBERT did not return hidden states")
    layer_index = config.HUBERT_HIDDEN_LAYER
    if layer_index < 0:
        layer_index += len(hidden_states)
    layer_index = min(max(layer_index, 0), len(hidden_states) - 1)
    hidden = hidden_states[layer_index][0].detach().float().cpu().numpy()
    if hidden.ndim != 2 or hidden.shape[0] == 0 or not np.all(np.isfinite(hidden)):
        raise RuntimeError("HuBERT returned invalid hidden states")
    embedding = np.mean(hidden, axis=0).astype(np.float32)
    if hidden.shape[0] > 1:
        frame_deltas = np.diff(hidden, axis=0)
        instability = float(np.mean(np.linalg.norm(frame_deltas, axis=1)) / math.sqrt(hidden.shape[1]))
    else:
        instability = 0.0
    elapsed_ms = int((time.perf_counter() - t0) * 1000)

    return HubertObservation(
        label=label,
        confidence=round(float(confidence), 6),
        probabilities={key: round(value, 6) for key, value in probabilities.items()},
        normalized_entropy=round(normalized_entropy, 6),
        top_two_margin=round(float(confidence - runner_up), 6),
        embedding=embedding,
        frame_instability=round(instability, 6),
        elapsed_ms=elapsed_ms,
        model_id=config.HUBERT_MODEL_ID,
    )


def analyze(audio_f32_16k: np.ndarray) -> HubertObservation:
    """Return the full local HuBERT observation for 16 kHz mono float audio."""
    load()
    return _analyze_impl(audio_f32_16k)


def classify(audio_f32_16k: np.ndarray) -> tuple[str, float, int]:
    """Compatibility wrapper returning the legacy label/confidence tuple."""
    observation = analyze(audio_f32_16k)
    return observation.label, observation.confidence, observation.elapsed_ms
