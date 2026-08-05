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


def _analyze_impl(audio_f32_16k: np.ndarray) -> HubertObservation:
    audio = np.asarray(audio_f32_16k, dtype=np.float32).reshape(-1)
    if audio.size == 0:
        raise ValueError("HuBERT requires non-empty audio")
    if not np.all(np.isfinite(audio)):
        raise ValueError("HuBERT audio contains non-finite samples")

    maximum_samples = int(round(config.HUBERT_MAX_SECONDS * 16000))
    if audio.size > maximum_samples:
        audio = audio[:maximum_samples]

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
