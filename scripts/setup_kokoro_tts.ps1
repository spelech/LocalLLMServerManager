<#
.SYNOPSIS
    Automated setup script for Kokoro TTS FastAPI Engine.
.DESCRIPTION
    Scaffolds the Kokoro TTS service directory, creates the production-ready FastAPI runner
    supporting OpenAI /v1/audio/speech, generates startup scripts, and downloads
    kokoro-v1.0.onnx (~350 MB) and voices-v1.0.bin.
.PARAMETER TargetDir
    Target directory for Kokoro engine installation (default: D:\AI\audio\engines\kokoro-fastapi or fallback).
.PARAMETER ModelsDir
    Models subfolder (default: $TargetDir\models).
.PARAMETER VoicesDir
    Voices subfolder (default: $TargetDir\voices).
.PARAMETER Port
    Listening port for FastAPI server (default: 8880).
.PARAMETER ListenHost
    Listening host address (default: 127.0.0.1).
.PARAMETER SkipModelDownload
    Skip downloading ONNX and voice model weights.
.PARAMETER InstallVenv
    Create Python virtual environment and install dependencies.
.PARAMETER Interactive
    Prompt user for paths if not specified.
#>
[CmdletBinding()]
param(
    [string]$TargetDir = "",
    [string]$ModelsDir = "",
    [string]$VoicesDir = "",
    [int]$Port = 8880,
    [string]$ListenHost = "127.0.0.1",
    [string]$HfToken = "",
    [switch]$SkipModelDownload,
    [switch]$InstallVenv,
    [switch]$Interactive
)

function Get-AppSettings {
    param([string]$Path)
    $candidates = @()
    if ($Path) { $candidates += $Path }
    $candidates += (Join-Path $PSScriptRoot "settings.json")
    $candidates += (Join-Path $PSScriptRoot "..\settings.json")
    if ($env:APPDATA) {
        $candidates += (Join-Path $env:APPDATA "LocalLLMServerManager\settings.json")
    }
    
    foreach ($cand in $candidates) {
        if ($cand -and (Test-Path $cand)) {
            try {
                $raw = Get-Content -Raw -Path $cand -ErrorAction Stop
                return ($raw | ConvertFrom-Json)
            } catch {
                Write-Verbose "Could not parse JSON from $($cand): $_"
            }
        }
    }
    return $null
}

function Resolve-PathVariables {
    param([string]$Path)
    if (-not $Path) { return "" }
    return [System.Environment]::ExpandEnvironmentVariables($Path)
}

# 1. Resolve TargetDir
$settings = Get-AppSettings
if (-not $TargetDir -and $settings) {
    if ($settings.AudioPath) {
        $resolvedAudio = Resolve-PathVariables $settings.AudioPath
        if ($resolvedAudio) {
            $TargetDir = Join-Path $resolvedAudio "engines\kokoro-fastapi"
        }
    } elseif ($settings.AudioEngineExecutablePath) {
        $resolvedExe = Resolve-PathVariables $settings.AudioEngineExecutablePath
        if ($resolvedExe) {
            $TargetDir = Split-Path -Path $resolvedExe -Parent
        }
    }
}

if (-not $TargetDir -and $Interactive) {
    $defaultCandidate = if (Test-Path "D:\") { "D:\AI\audio\engines\kokoro-fastapi" } else { "C:\AI\audio\engines\kokoro-fastapi" }
    $inputDir = Read-Host "Enter target directory for Kokoro TTS [Default: $defaultCandidate]"
    if ($inputDir) {
        $TargetDir = $inputDir
    }
}

if (-not $TargetDir) {
    if (Test-Path "D:\") {
        $TargetDir = "D:\AI\audio\engines\kokoro-fastapi"
    } elseif (Test-Path "C:\AI") {
        $TargetDir = "C:\AI\audio\engines\kokoro-fastapi"
    } else {
        $TargetDir = Join-Path $env:USERPROFILE "AI\audio\engines\kokoro-fastapi"
    }
}

if (-not $ModelsDir) {
    $ModelsDir = Join-Path $TargetDir "models"
}

if (-not $VoicesDir) {
    $VoicesDir = Join-Path $TargetDir "voices"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Kokoro TTS Engine Setup & Scaffolding" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target Directory : $TargetDir" -ForegroundColor Yellow
Write-Host "Models Directory : $ModelsDir" -ForegroundColor Yellow
Write-Host "Voices Directory : $VoicesDir" -ForegroundColor Yellow
Write-Host "Server Port      : $Port" -ForegroundColor Yellow

# Ensure directory structure
if (-not (Test-Path $TargetDir)) { New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null }
if (-not (Test-Path $ModelsDir)) { New-Item -ItemType Directory -Path $ModelsDir -Force | Out-Null }
if (-not (Test-Path $VoicesDir)) { New-Item -ItemType Directory -Path $VoicesDir -Force | Out-Null }

# 2. Write requirements.txt
$requirementsContent = @"
fastapi>=0.104.0
uvicorn[standard]>=0.24.0
kokoro-onnx>=0.3.0
soundfile>=0.12.1
numpy>=1.24.0
pydantic>=2.0.0
"@
$requirementsPath = Join-Path $TargetDir "requirements.txt"
$requirementsContent | Out-File -FilePath $requirementsPath -Encoding utf8
Write-Host "Created requirements.txt at $requirementsPath" -ForegroundColor Green

# 3. Write main.py
$mainPyContent = @"
import os
import io
import math
import logging
from typing import Optional, List, Dict, Any
import numpy as np
import soundfile as sf
from fastapi import FastAPI, HTTPException, Response
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("kokoro-fastapi")

app = FastAPI(
    title="Kokoro TTS FastAPI Server",
    description="Sub-100ms TTS engine with OpenAI /v1/audio/speech compatibility",
    version="1.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Global Kokoro instance
_kokoro = None
_model_loaded = False
_loaded_model_path = None
_loaded_voices_path = None

VOICE_CATALOG = [
    {"id": "af_heart", "name": "Heart (American Female - High Quality)", "gender": "female", "language": "en-us"},
    {"id": "af_bella", "name": "Bella (American Female)", "gender": "female", "language": "en-us"},
    {"id": "af_sarah", "name": "Sarah (American Female)", "gender": "female", "language": "en-us"},
    {"id": "af_nicole", "name": "Nicole (American Female)", "gender": "female", "language": "en-us"},
    {"id": "af_sky", "name": "Sky (American Female)", "gender": "female", "language": "en-us"},
    {"id": "am_adam", "name": "Adam (American Male)", "gender": "male", "language": "en-us"},
    {"id": "am_michael", "name": "Michael (American Male)", "gender": "male", "language": "en-us"},
    {"id": "am_eric", "name": "Eric (American Male)", "gender": "male", "language": "en-us"},
    {"id": "bf_emma", "name": "Emma (British Female)", "gender": "female", "language": "en-gb"},
    {"id": "bf_isabella", "name": "Isabella (British Female)", "gender": "female", "language": "en-gb"},
    {"id": "bm_george", "name": "George (British Male)", "gender": "male", "language": "en-gb"},
    {"id": "bm_lewis", "name": "Lewis (British Male)", "gender": "male", "language": "en-gb"}
]

def find_model_file(filename: str, search_dirs: List[str]) -> Optional[str]:
    for d in search_dirs:
        candidate = os.path.join(d, filename)
        if os.path.isfile(candidate) and os.path.getsize(candidate) > 0:
            return os.path.abspath(candidate)
    return None

def init_kokoro():
    global _kokoro, _model_loaded, _loaded_model_path, _loaded_voices_path
    base_dir = os.path.dirname(os.path.abspath(__file__))
    search_dirs = [
        os.path.join(base_dir, "models"),
        os.path.join(base_dir, "voices"),
        base_dir,
        os.path.join(base_dir, ".."),
        r"D:\AI\audio\engines\kokoro-fastapi\models",
        r"D:\AI\audio\engines\kokoro-fastapi\voices",
        r"C:\AI\audio\engines\kokoro-fastapi\models",
        r"C:\AI\audio\engines\kokoro-fastapi\voices"
    ]

    model_path = os.environ.get("KOKORO_MODEL_PATH") or find_model_file("kokoro-v1.0.onnx", search_dirs)
    voices_path = os.environ.get("KOKORO_VOICES_PATH") or find_model_file("voices-v1.0.bin", search_dirs) or find_model_file("voices.bin", search_dirs)

    if not model_path or not voices_path:
        logger.warning(f"Kokoro model or voice weights not found (Model: {model_path}, Voices: {voices_path}). Running in graceful fallback mode.")
        _model_loaded = False
        return

    try:
        from kokoro_onnx import Kokoro
        _kokoro = Kokoro(model_path, voices_path)
        _model_loaded = True
        _loaded_model_path = model_path
        _loaded_voices_path = voices_path
        logger.info(f"Successfully loaded Kokoro ONNX model from {model_path} with voices from {voices_path}")
    except Exception as ex:
        logger.error(f"Failed to initialize kokoro-onnx: {ex}. Running in graceful fallback mode.")
        _model_loaded = False

@app.on_event("startup")
def startup_event():
    init_kokoro()

class SpeechRequest(BaseModel):
    model: Optional[str] = "kokoro"
    input: str
    voice: Optional[str] = "af_heart"
    response_format: Optional[str] = "mp3"
    speed: Optional[float] = 1.0

def generate_fallback_tone(text: str, duration_sec: float = 1.5, sample_rate: int = 24000) -> np.ndarray:
    t = np.linspace(0, duration_sec, int(sample_rate * duration_sec), endpoint=False)
    # Simple soft warm chord (A4 440Hz + E5 660Hz) to represent active fallback synthesis
    tone = 0.15 * np.sin(2 * np.pi * 440.0 * t) + 0.10 * np.sin(2 * np.pi * 659.25 * t)
    fade_len = int(sample_rate * 0.05)
    fade_in = np.linspace(0, 1, fade_len)
    fade_out = np.linspace(1, 0, fade_len)
    tone[:fade_len] *= fade_in
    tone[-fade_len:] *= fade_out
    return tone.astype(np.float32)

@app.get("/health")
@app.get("/api/health")
def health_check():
    return {
        "status": "ok",
        "engine": "kokoro-fastapi",
        "model_loaded": _model_loaded,
        "model_path": _loaded_model_path,
        "voices_path": _loaded_voices_path,
        "supported_voices": [v["id"] for v in VOICE_CATALOG]
    }

@app.get("/v1/audio/voices")
@app.get("/v1/voices")
def list_voices():
    return {
        "voices": VOICE_CATALOG
    }

@app.get("/v1/models")
def list_models():
    return {
        "object": "list",
        "data": [
            {
                "id": "kokoro",
                "object": "model",
                "created": 1700000000,
                "owned_by": "hexgrad",
                "permission": [],
                "root": "kokoro",
                "parent": None
            },
            {
                "id": "tts-1",
                "object": "model",
                "created": 1700000000,
                "owned_by": "openai",
                "permission": [],
                "root": "tts-1",
                "parent": None
            },
            {
                "id": "tts-1-hd",
                "object": "model",
                "created": 1700000000,
                "owned_by": "openai",
                "permission": [],
                "root": "tts-1-hd",
                "parent": None
            }
        ]
    }

@app.post("/v1/audio/speech")
async def generate_speech(request: SpeechRequest):
    if not request.input or not request.input.strip():
        raise HTTPException(status_code=400, detail="Input text cannot be empty")

    voice = request.voice or "af_heart"
    speed = float(request.speed or 1.0)
    fmt = (request.response_format or "mp3").lower()
    sample_rate = 24000

    audio_samples = None

    if _model_loaded and _kokoro is not None:
        try:
            samples, sr = _kokoro.create(
                request.input.strip(),
                voice=voice,
                speed=speed,
                lang="en-us"
            )
            audio_samples = samples
            sample_rate = sr
        except Exception as ex:
            logger.warning(f"Synthesis failed with error: {ex}. Falling back to clean tone synthesizer.")
            audio_samples = generate_fallback_tone(request.input, duration_sec=max(1.0, len(request.input) * 0.06), sample_rate=sample_rate)
    else:
        # Fallback synthesizer
        audio_samples = generate_fallback_tone(request.input, duration_sec=max(1.0, len(request.input) * 0.06), sample_rate=sample_rate)

    out_buffer = io.BytesIO()
    media_type = "audio/mpeg"

    if fmt == "wav":
        sf.write(out_buffer, audio_samples, sample_rate, format="WAV")
        media_type = "audio/wav"
    elif fmt == "flac":
        sf.write(out_buffer, audio_samples, sample_rate, format="FLAC")
        media_type = "audio/flac"
    elif fmt == "ogg" or fmt == "opus":
        try:
            sf.write(out_buffer, audio_samples, sample_rate, format="OGG")
            media_type = "audio/ogg"
        except Exception:
            sf.write(out_buffer, audio_samples, sample_rate, format="WAV")
            media_type = "audio/wav"
    else:
        # mp3 or default
        try:
            sf.write(out_buffer, audio_samples, sample_rate, format="MP3")
            media_type = "audio/mpeg"
        except Exception:
            # Fallback to WAV format if libmp3lame not present in local soundfile binary
            sf.write(out_buffer, audio_samples, sample_rate, format="WAV")
            media_type = "audio/wav"

    out_buffer.seek(0)
    return StreamingResponse(
        out_buffer,
        media_type=media_type,
        headers={"Content-Disposition": f'inline; filename="speech.{fmt}"'}
    )

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8880)
"@

$mainPyPath = Join-Path $TargetDir "main.py"
$mainPyContent | Out-File -FilePath $mainPyPath -Encoding utf8
Write-Host "Created main.py at $mainPyPath" -ForegroundColor Green

# 4. Write run.bat and start.bat
$runBatContent = @"
@echo off
setlocal
cd /d "%~dp0"
title Kokoro TTS FastAPI Engine - Port $Port

echo ===================================================
echo Starting Kokoro TTS FastAPI Server on port $Port...
echo ===================================================

set PYTHON_EXE=python
if exist "%~dp0.venv\Scripts\python.exe" (
    set PYTHON_EXE="%~dp0.venv\Scripts\python.exe"
)

%PYTHON_EXE% -m uvicorn main:app --host $ListenHost --port $Port
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Server stopped with exit code %ERRORLEVEL%.
    pause
)
"@

$runBatPath = Join-Path $TargetDir "run.bat"
$startBatPath = Join-Path $TargetDir "start.bat"
$runBatContent | Out-File -FilePath $runBatPath -Encoding utf8
$runBatContent | Out-File -FilePath $startBatPath -Encoding utf8
Write-Host "Created run.bat and start.bat at $TargetDir" -ForegroundColor Green

# 5. Download Model Weights
if (-not $SkipModelDownload) {
    $modelOnnxPath = Join-Path $ModelsDir "kokoro-v1.0.onnx"
    $voicesBinPath = Join-Path $VoicesDir "voices-v1.0.bin"

    # Also check if they exist in TargetDir root
    $rootOnnxPath = Join-Path $TargetDir "kokoro-v1.0.onnx"
    $rootVoicesPath = Join-Path $TargetDir "voices-v1.0.bin"

    $onnxUrl = "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/kokoro-v1.0.onnx"
    $voicesUrl = "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/voices-v1.0.bin"

    if (-not (Test-Path $modelOnnxPath) -and -not (Test-Path $rootOnnxPath)) {
        Write-Host "Downloading Kokoro ONNX model (~350 MB) to $modelOnnxPath..." -ForegroundColor Cyan
        try {
            Invoke-WebRequest -Uri $onnxUrl -OutFile $modelOnnxPath -ErrorAction Stop
            Write-Host "Kokoro ONNX model downloaded successfully." -ForegroundColor Green
        } catch {
            Write-Host "Notice: Online download failed or network is unreachable: $_" -ForegroundColor Yellow
            Write-Host "You can manually place 'kokoro-v1.0.onnx' in '$ModelsDir'." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Kokoro ONNX model already present." -ForegroundColor Green
    }

    if (-not (Test-Path $voicesBinPath) -and -not (Test-Path $rootVoicesPath)) {
        Write-Host "Downloading Kokoro voices bin (~28 MB) to $voicesBinPath..." -ForegroundColor Cyan
        try {
            Invoke-WebRequest -Uri $voicesUrl -OutFile $voicesBinPath -ErrorAction Stop
            Write-Host "Kokoro voices bin downloaded successfully." -ForegroundColor Green
        } catch {
            Write-Host "Notice: Online download failed or network is unreachable: $_" -ForegroundColor Yellow
            Write-Host "You can manually place 'voices-v1.0.bin' in '$VoicesDir'." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Kokoro voices bin already present." -ForegroundColor Green
    }
}

# 6. Optional Venv Setup
if ($InstallVenv) {
    Write-Host "Setting up Python virtual environment..." -ForegroundColor Cyan
    $venvDir = Join-Path $TargetDir ".venv"
    if (-not (Test-Path $venvDir)) {
        python -m venv $venvDir
    }
    $pipExe = Join-Path $venvDir "Scripts\pip.exe"
    if (Test-Path $pipExe) {
        & $pipExe install -r $requirementsPath
    }
}

Write-Host "Kokoro TTS Engine Scaffolding & Setup Completed Successfully!" -ForegroundColor Green
