# syntax=docker/dockerfile:1
FROM python:3.12-slim

# ElevenLabs can fall back from PCM to MP3; pydub needs ffmpeg to decode it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

ENV PYTHONUNBUFFERED=1 \
    HF_HOME=/opt/huggingface

ARG HUBERT_MODEL_ID=superb/hubert-base-superb-er
ARG HUBERT_MODEL_REVISION=9a456581e0147a2b7fdaf56d77a9e8fce3865eaa
ENV HUBERT_MODEL_ID=${HUBERT_MODEL_ID} \
    HUBERT_MODEL_REVISION=${HUBERT_MODEL_REVISION} \
    HUBERT_LOCAL_FILES_ONLY=true

WORKDIR /app

# Install the CPU wheel explicitly so Cloud Run never pulls CUDA packages.
COPY requirements.txt .
RUN pip install --no-cache-dir --index-url https://download.pytorch.org/whl/cpu torch \
    && pip install --no-cache-dir -r requirements.txt

# Bake HuBERT into the image instead of downloading it during a cold start.
RUN python -c "\
import os; \
from transformers import AutoFeatureExtractor, AutoModelForAudioClassification; \
model_id=os.environ['HUBERT_MODEL_ID']; \
revision=os.environ['HUBERT_MODEL_REVISION']; \
AutoFeatureExtractor.from_pretrained(model_id, revision=revision); \
AutoModelForAudioClassification.from_pretrained(model_id, revision=revision, use_safetensors=True)"

COPY . .

# Cloud Run injects PORT and requires binding to every container interface.
ENV PORT=8080
CMD ["sh", "-c", "exec uvicorn app:app --host 0.0.0.0 --port ${PORT}"]
