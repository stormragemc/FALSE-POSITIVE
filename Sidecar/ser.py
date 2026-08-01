"""Speech emotion recognition via a local HuBERT checkpoint — local, free, no
API key.

`hubert-base-superb-er` is the v1 default over `hubert-large-superb-er`: the
turn-latency budget is the single biggest risk to this feature (see plan
section 0), and the base checkpoint is roughly a third of the size for a
similar accuracy band on this 4-class task. Both load through this same
module's interface, so upgrading to the large checkpoint later is a one-line
change to _MODEL_ID.

This is a weak, 4-class signal (~0.68 accuracy on IEMOCAP at the -large
checkpoint) — treat the output as a soft impression, not ground truth. The
caller (llm.py) is responsible for framing it that way to the model.
"""

import time

import numpy as np
import torch
from transformers import AutoFeatureExtractor, AutoModelForAudioClassification

_MODEL_ID = "superb/hubert-base-superb-er"

# Confirmed by smoke-testing the actual model: its id2label uses IEMOCAP's
# abbreviated class names ("hap", not "happy"), which reads oddly dropped
# straight into an LLM prompt. Expand to the words the prompt in llm.py
# actually expects.
_LABEL_DISPLAY_NAMES = {
    "neu": "neutral",
    "hap": "happy",
    "ang": "angry",
    "sad": "sad",
}

_feature_extractor = None
_model = None


def load():
    global _feature_extractor, _model
    if _model is None:
        print(f"[Sidecar] Loading emotion model '{_MODEL_ID}' (downloads on first run)...")
        _feature_extractor = AutoFeatureExtractor.from_pretrained(_MODEL_ID)
        _model = AutoModelForAudioClassification.from_pretrained(_MODEL_ID)
        _model.eval()
        # Warm up the forward pass once.
        _classify_impl(np.zeros(16000, dtype=np.float32))
        print("[Sidecar] Emotion model ready.")
    return _feature_extractor, _model


def _classify_impl(audio_f32_16k: np.ndarray) -> tuple[str, float]:
    inputs = _feature_extractor(audio_f32_16k, sampling_rate=16000, return_tensors="pt")
    with torch.no_grad():
        logits = _model(**inputs).logits
    probs = torch.softmax(logits, dim=-1)[0]
    idx = int(torch.argmax(probs).item())
    raw_label = _model.config.id2label[idx]
    label = _LABEL_DISPLAY_NAMES.get(raw_label, raw_label)
    confidence = float(probs[idx].item())
    return label, confidence


def classify(audio_f32_16k: np.ndarray) -> tuple[str, float, int]:
    """audio_f32_16k: mono float32 samples at 16kHz.
    Returns (label, confidence, elapsed_ms)."""
    load()
    t0 = time.perf_counter()
    label, confidence = _classify_impl(audio_f32_16k)
    ms = int((time.perf_counter() - t0) * 1000)
    return label, confidence, ms
