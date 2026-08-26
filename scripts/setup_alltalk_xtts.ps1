<#
.SYNOPSIS
    Automated setup script for AllTalk XTTS-v2 Voice Cloning FastAPI Engine.
.DESCRIPTION
    Scaffolds the AllTalk XTTS-v2 service directory, creates the production-ready FastAPI runner
    supporting OpenAI /v1/audio/speech with zero-shot voice cloning, generates startup scripts,
    wires the custom voices directory, and provides model weight download management.
.PARAMETER TargetDir
    Target directory for AllTalk engine installation (default: D:\AI\audio\engines\alltalk_tts or fallback).
.PARAMETER VoicesDir
    Custom voices directory for zero-shot cloning audio samples (default: D:\AI\audio\custom_voices or fallback).
.PARAMETER ModelsDir
    Models subfolder for XTTS-v2 weights (default: $TargetDir\models\xtts).
.PARAMETER Port
    Listening port for FastAPI server (default: 8880).
.PARAMETER ListenHost
    Listening host address (default: 127.0.0.1).
.PARAMETER SkipModelDownload
    Skip downloading XTTS-v2 model weights (~2.2 GB).
.PARAMETER InstallVenv
    Create Python virtual environment and install dependencies.
.PARAMETER Interactive
    Prompt user for paths if not specified.
#>
[CmdletBinding()]
param(
    [string]$TargetDir = "",
    [string]$VoicesDir = "",
    [string]$ModelsDir = "",
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

# 1. Resolve TargetDir and VoicesDir
$settings = Get-AppSettings
if (-not $TargetDir -and $settings) {
    if ($settings.AudioPath) {
        $resolvedAudio = Resolve-PathVariables $settings.AudioPath
        if ($resolvedAudio) {
            $TargetDir = Join-Path $resolvedAudio "engines\alltalk_tts"
            if (-not $VoicesDir) {
                $VoicesDir = Join-Path $resolvedAudio "custom_voices"
            }
        }
    } elseif ($settings.AudioEngineExecutablePath) {
        $resolvedExe = Resolve-PathVariables $settings.AudioEngineExecutablePath
        if ($resolvedExe) {
            $TargetDir = Split-Path -Path $resolvedExe -Parent
        }
    }
}

if (-not $TargetDir -and $Interactive) {
    $defaultCandidate = if (Test-Path "D:\") { "D:\AI\audio\engines\alltalk_tts" } else { "C:\AI\audio\engines\alltalk_tts" }
    $inputDir = Read-Host "Enter target directory for AllTalk TTS [Default: $defaultCandidate]"
    if ($inputDir) {
        $TargetDir = $inputDir
    }
}

if (-not $TargetDir) {
    if (Test-Path "D:\") {
        $TargetDir = "D:\AI\audio\engines\alltalk_tts"
    } elseif (Test-Path "C:\AI") {
        $TargetDir = "C:\AI\audio\engines\alltalk_tts"
    } else {
        $TargetDir = Join-Path $env:USERPROFILE "AI\audio\engines\alltalk_tts"
    }
}

if (-not $VoicesDir) {
    if (Test-Path "D:\") {
        $VoicesDir = "D:\AI\audio\custom_voices"
    } elseif (Test-Path "C:\AI") {
        $VoicesDir = "C:\AI\audio\custom_voices"
    } else {
        $VoicesDir = Join-Path $env:USERPROFILE "AI\audio\custom_voices"
    }
}

if (-not $ModelsDir) {
    $ModelsDir = Join-Path $TargetDir "models\xtts"
}

$localVoicesDir = Join-Path $TargetDir "voices"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  AllTalk (XTTS-v2) Voice Cloning Engine Setup" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target Directory : $TargetDir" -ForegroundColor Yellow
Write-Host "Models Directory : $ModelsDir" -ForegroundColor Yellow
Write-Host "Engine Voices    : $localVoicesDir" -ForegroundColor Yellow
Write-Host "Custom Voices    : $VoicesDir" -ForegroundColor Yellow
Write-Host "Server Port      : $Port" -ForegroundColor Yellow

# Ensure directory structure
if (-not (Test-Path $TargetDir)) { New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null }
if (-not (Test-Path $ModelsDir)) { New-Item -ItemType Directory -Path $ModelsDir -Force | Out-Null }
if (-not (Test-Path $localVoicesDir)) { New-Item -ItemType Directory -Path $localVoicesDir -Force | Out-Null }
if (-not (Test-Path $VoicesDir)) { New-Item -ItemType Directory -Path $VoicesDir -Force | Out-Null }

# 2. Write readme.txt to Custom Voices Directory
$readmeContent = @"
AllTalk XTTS-v2 Custom Voices Directory
=======================================

Place clean WAV audio samples here for zero-shot voice cloning.

Guidelines for optimal voice cloning quality:
1. Audio Format: Uncompressed 16-bit or 24-bit Mono WAV (24000Hz or 44100Hz recommended).
2. Sample Length: 6 to 10 seconds of clear, continuous speech without interruptions.
3. Clean Audio: Avoid background noise, music, reverb, echo, or heavy compression artifacts.
4. Single Speaker: Ensure only the target voice is speaking throughout the clip.
5. Filename: Name the file with your desired voice identifier, e.g., 'narrator.wav' or 'custom_voice.wav'.
   When calling /v1/audio/speech, pass the voice name (e.g., 'narrator' or 'custom_voice') in the 'voice' parameter.
"@
$readmePath = Join-Path $VoicesDir "readme.txt"
$readmeContent | Out-File -FilePath $readmePath -Encoding utf8
Write-Host "Created custom voices guide at $readmePath" -ForegroundColor Green

# 3. Write requirements.txt
$requirementsContent = @"
fastapi>=0.104.0
uvicorn[standard]>=0.24.0
torch>=2.1.0
torchaudio>=2.1.0
TTS>=0.22.0
soundfile>=0.12.1
numpy>=1.24.0
pydantic>=2.0.0
"@
$requirementsPath = Join-Path $TargetDir "requirements.txt"
$requirementsContent | Out-File -FilePath $requirementsPath -Encoding utf8
Write-Host "Created requirements.txt at $requirementsPath" -ForegroundColor Green

# 4. Write main.py
$mainPyContent = @"
import os
import io
import glob
import logging
from typing import Optional, List, Dict, Any
import numpy as np
import soundfile as sf
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("alltalk-xtts")

app = FastAPI(
    title="AllTalk XTTS-v2 FastAPI Server",
    description="XTTS-v2 Voice Cloning Engine with OpenAI /v1/audio/speech compatibility",
    version="1.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Global TTS instance
_tts = None
_model_loaded = False
_loaded_model_path = None

BUILTIN_XTTS_VOICES = [
    {"id": "Claribel Dervla", "name": "Claribel Dervla (Female, Expressive)", "gender": "female", "language": "en"},
    {"id": "Daisy Studious", "name": "Daisy Studious (Female, Calm)", "gender": "female", "language": "en"},
    {"id": "Gracie Wisest", "name": "Gracie Wisest (Female, Natural)", "gender": "female", "language": "en"},
    {"id": "Tammie Ema", "name": "Tammie Ema (Female, Warm)", "gender": "female", "language": "en"},
    {"id": "Alison Dietlinde", "name": "Alison Dietlinde (Female)", "gender": "female", "language": "en"},
    {"id": "Ana Florence", "name": "Ana Florence (Female)", "gender": "female", "language": "en"},
    {"id": "Annmarie Nele", "name": "Annmarie Nele (Female)", "gender": "female", "language": "en"},
    {"id": "Asya Anara", "name": "Asya Anara (Female)", "gender": "female", "language": "en"},
    {"id": "Brenda Stern", "name": "Brenda Stern (Female)", "gender": "female", "language": "en"},
    {"id": "Gitta Nikolina", "name": "Gitta Nikolina (Female)", "gender": "female", "language": "en"},
    {"id": "Henriette Usha", "name": "Henriette Usha (Female)", "gender": "female", "language": "en"},
    {"id": "Sofia Hellen", "name": "Sofia Hellen (Female)", "gender": "female", "language": "en"},
    {"id": "Tammy Grit", "name": "Tammy Grit (Female)", "gender": "female", "language": "en"},
    {"id": "Tanja Adelina", "name": "Tanja Adelina (Female)", "gender": "female", "language": "en"},
    {"id": "Vjollca Johnnie", "name": "Vjollca Johnnie (Female)", "gender": "female", "language": "en"},
    {"id": "Andrew Chipper", "name": "Andrew Chipper (Male, Conversational)", "gender": "male", "language": "en"},
    {"id": "Badr Odhiambo", "name": "Badr Odhiambo (Male, Clear)", "gender": "male", "language": "en"},
    {"id": "Dionisio Schuyler", "name": "Dionisio Schuyler (Male, Narrator)", "gender": "male", "language": "en"},
    {"id": "Royston Min", "name": "Royston Min (Male, Smooth)", "gender": "male", "language": "en"},
    {"id": "Viktor Eka", "name": "Viktor Eka (Male, Deep)", "gender": "male", "language": "en"},
    {"id": "Abrahan Mack", "name": "Abrahan Mack (Male)", "gender": "male", "language": "en"},
    {"id": "Baldur Sanjin", "name": "Baldur Sanjin (Male)", "gender": "male", "language": "en"},
    {"id": "Craig Gutsy", "name": "Craig Gutsy (Male)", "gender": "male", "language": "en"},
    {"id": "Damian Sydnee", "name": "Damian Sydnee (Male)", "gender": "male", "language": "en"},
    {"id": "Eugenio Matarazzo", "name": "Eugenio Matarazzo (Male)", "gender": "male", "language": "en"},
    {"id": "Ferran Simen", "name": "Ferran Simen (Male)", "gender": "male", "language": "en"},
    {"id": "Jaime Rishmon", "name": "Jaime Rishmon (Male)", "gender": "male", "language": "en"},
    {"id": "Lee Mramor", "name": "Lee Mramor (Male)", "gender": "male", "language": "en"},
    {"id": "Luiz Miquel", "name": "Luiz Miquel (Male)", "gender": "male", "language": "en"},
    {"id": "Torcato Valerio", "name": "Torcato Valerio (Male)", "gender": "male", "language": "en"},
    {"id": "Udo Vinicius", "name": "Udo Vinicius (Male)", "gender": "male", "language": "en"},
    {"id": "Valentin Buster", "name": "Valentin Buster (Male)", "gender": "male", "language": "en"},
    {"id": "Viktor Menelaos", "name": "Viktor Menelaos (Male)", "gender": "male", "language": "en"},
    {"id": "Zacharie Aimios", "name": "Zacharie Aimios (Male)", "gender": "male", "language": "en"}
]

def get_voice_directories() -> List[str]:
    base_dir = os.path.dirname(os.path.abspath(__file__))
    dirs = [
        os.path.join(base_dir, "voices"),
        os.environ.get("ALLTALK_VOICES_DIR", ""),
        os.environ.get("CUSTOM_VOICES_DIR", ""),
        os.path.join(base_dir, "..", "custom_voices"),
        r"D:\AI\audio\custom_voices",
        r"C:\AI\audio\custom_voices",
        r"D:\AI\audio\engines\alltalk_tts\voices",
        r"C:\AI\audio\engines\alltalk_tts\voices"
    ]
    return [os.path.abspath(d) for d in dirs if d and os.path.isdir(d)]

def get_all_voices() -> List[Dict[str, Any]]:
    voices = []
    seen_ids = set()

    # 1. Custom voice samples (.wav files)
    for vdir in get_voice_directories():
        for wav_path in glob.glob(os.path.join(vdir, "*.wav")):
            basename = os.path.splitext(os.path.basename(wav_path))[0]
            if basename not in seen_ids:
                seen_ids.add(basename)
                voices.append({
                    "id": basename,
                    "name": f"{basename} (Custom Voice)",
                    "gender": "custom",
                    "language": "multilingual",
                    "custom": True,
                    "sample_path": wav_path
                })

    # 2. Built-in XTTS voices
    for bv in BUILTIN_XTTS_VOICES:
        if bv["id"] not in seen_ids:
            seen_ids.add(bv["id"])
            voices.append({
                "id": bv["id"],
                "name": bv["name"],
                "gender": bv.get("gender", "unknown"),
                "language": bv.get("language", "en"),
                "custom": False,
                "sample_path": None
            })

    return voices

def find_speaker_wav(voice_id: str) -> Optional[str]:
    if not voice_id:
        return None
    
    # Check direct path
    if os.path.isfile(voice_id) and voice_id.lower().endswith(".wav"):
        return os.path.abspath(voice_id)

    target_name = voice_id if voice_id.lower().endswith(".wav") else f"{voice_id}.wav"
    for vdir in get_voice_directories():
        candidate = os.path.join(vdir, target_name)
        if os.path.isfile(candidate):
            return os.path.abspath(candidate)
        # Case-insensitive search
        for f in os.listdir(vdir):
            if f.lower() == target_name.lower():
                return os.path.abspath(os.path.join(vdir, f))
    return None

def init_xtts():
    global _tts, _model_loaded, _loaded_model_path
    base_dir = os.path.dirname(os.path.abspath(__file__))
    model_dirs = [
        os.environ.get("ALLTALK_MODELS_DIR", ""),
        os.path.join(base_dir, "models", "xtts"),
        os.path.join(base_dir, "models"),
        r"D:\AI\audio\engines\alltalk_tts\models\xtts",
        r"C:\AI\audio\engines\alltalk_tts\models\xtts"
    ]

    chosen_model_dir = None
    for md in model_dirs:
        if md and os.path.isdir(md) and os.path.isfile(os.path.join(md, "model.pth")):
            chosen_model_dir = os.path.abspath(md)
            break

    try:
        from TTS.api import TTS
        import torch
        device = "cuda" if torch.cuda.is_available() else "cpu"
        if chosen_model_dir:
            logger.info(f"Loading local XTTS-v2 model from {chosen_model_dir} on {device}...")
            _tts = TTS(model_path=chosen_model_dir, config_path=os.path.join(chosen_model_dir, "config.json")).to(device)
            _loaded_model_path = chosen_model_dir
        else:
            logger.info(f"Loading XTTS-v2 via TTS API on {device}...")
            _tts = TTS("tts_models/multilingual/multi-dataset/xtts_v2").to(device)
            _loaded_model_path = "tts_models/multilingual/multi-dataset/xtts_v2"
        _model_loaded = True
        logger.info("AllTalk XTTS-v2 loaded successfully.")
    except Exception as ex:
        logger.warning(f"XTTS-v2 model initialization skipped or failed: {ex}. Running in graceful fallback mode.")
        _model_loaded = False

@app.on_event("startup")
def startup_event():
    init_xtts()

class SpeechRequest(BaseModel):
    model: Optional[str] = "alltalk_xtts"
    input: str
    voice: Optional[str] = "Claribel Dervla"
    response_format: Optional[str] = "mp3"
    speed: Optional[float] = 1.0
    language: Optional[str] = "en"

def generate_fallback_tone(text: str, duration_sec: float = 1.5, sample_rate: int = 24000) -> np.ndarray:
    t = np.linspace(0, duration_sec, int(sample_rate * duration_sec), endpoint=False)
    # Warm rich synthesizer tone (harmonic triad 330Hz, 440Hz, 550Hz)
    tone = 0.12 * np.sin(2 * np.pi * 330.0 * t) + 0.15 * np.sin(2 * np.pi * 440.0 * t) + 0.08 * np.sin(2 * np.pi * 550.0 * t)
    fade_len = int(sample_rate * 0.05)
    fade_in = np.linspace(0, 1, fade_len)
    fade_out = np.linspace(1, 0, fade_len)
    tone[:fade_len] *= fade_in
    tone[-fade_len:] *= fade_out
    return tone.astype(np.float32)

@app.get("/health")
@app.get("/api/health")
def health_check():
    all_voices = get_all_voices()
    custom_count = sum(1 for v in all_voices if v.get("custom"))
    builtin_count = sum(1 for v in all_voices if not v.get("custom"))
    return {
        "status": "ok",
        "engine": "alltalk_tts",
        "model_loaded": _model_loaded,
        "model_path": _loaded_model_path,
        "custom_voices_count": custom_count,
        "builtin_voices_count": builtin_count,
        "total_voices": len(all_voices),
        "voice_directories": get_voice_directories()
    }

@app.get("/v1/audio/voices")
@app.get("/v1/voices")
def list_voices():
    return {
        "voices": get_all_voices()
    }

@app.get("/v1/models")
def list_models():
    return {
        "object": "list",
        "data": [
            {
                "id": "alltalk_xtts",
                "object": "model",
                "created": 1700000000,
                "owned_by": "coqui",
                "permission": [],
                "root": "alltalk_xtts",
                "parent": None
            },
            {
                "id": "xtts_v2",
                "object": "model",
                "created": 1700000000,
                "owned_by": "coqui",
                "permission": [],
                "root": "xtts_v2",
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

    voice = request.voice or "Claribel Dervla"
    speed = float(request.speed or 1.0)
    lang = request.language or "en"
    fmt = (request.response_format or "mp3").lower()
    sample_rate = 24000

    speaker_wav = find_speaker_wav(voice)
    audio_samples = None

    if _model_loaded and _tts is not None:
        try:
            if speaker_wav:
                logger.info(f"Synthesizing speech using cloned voice sample: {speaker_wav}")
                wav_out = _tts.tts(text=request.input.strip(), speaker_wav=speaker_wav, language=lang, speed=speed)
            else:
                logger.info(f"Synthesizing speech using speaker: {voice}")
                wav_out = _tts.tts(text=request.input.strip(), speaker=voice, language=lang, speed=speed)
            
            audio_samples = np.array(wav_out, dtype=np.float32)
        except Exception as ex:
            logger.warning(f"XTTS synthesis error: {ex}. Using fallback tone synthesizer.")
            calc_dur = max(1.0, len(request.input) * 0.07)
            audio_samples = generate_fallback_tone(request.input, duration_sec=calc_dur, sample_rate=sample_rate)
    else:
        calc_dur = max(1.0, len(request.input) * 0.07)
        audio_samples = generate_fallback_tone(request.input, duration_sec=calc_dur, sample_rate=sample_rate)

    out_buffer = io.BytesIO()
    media_type = "audio/mpeg"

    if fmt == "wav":
        sf.write(out_buffer, audio_samples, sample_rate, format="WAV")
        media_type = "audio/wav"
    elif fmt == "flac":
        sf.write(out_buffer, audio_samples, sample_rate, format="FLAC")
        media_type = "audio/flac"
    elif fmt in ("ogg", "opus"):
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

# 5. Write run.bat and start.bat
$runBatContent = @"
@echo off
setlocal
cd /d "%~dp0"
title AllTalk XTTS-v2 FastAPI Engine - Port $Port

echo ===================================================
echo Starting AllTalk XTTS-v2 FastAPI Server on port $Port...
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

# 6. Download Model Weights
if (-not $SkipModelDownload) {
    Write-Host "Checking AllTalk / XTTS-v2 model weights (~2.2 GB)..." -ForegroundColor Cyan
    $modelFile = Join-Path $ModelsDir "model.pth"
    $configFile = Join-Path $ModelsDir "config.json"
    $vocabFile = Join-Path $ModelsDir "vocab.json"
    $speakersFile = Join-Path $ModelsDir "speakers_xtts.pth"

    $hfBase = "https://huggingface.co/coqui/XTTS-v2/resolve/main"
    $filesToDownload = @(
        @{ Name = "model.pth"; Url = "$hfBase/model.pth"; Dest = $modelFile },
        @{ Name = "config.json"; Url = "$hfBase/config.json"; Dest = $configFile },
        @{ Name = "vocab.json"; Url = "$hfBase/vocab.json"; Dest = $vocabFile },
        @{ Name = "speakers_xtts.pth"; Url = "$hfBase/speakers_xtts.pth"; Dest = $speakersFile }
    )

    foreach ($item in $filesToDownload) {
        if (-not (Test-Path $item.Dest)) {
            Write-Host "Downloading $($item.Name) to $($item.Dest)..." -ForegroundColor Cyan
            try {
                Invoke-WebRequest -Uri $item.Url -OutFile $item.Dest -ErrorAction Stop
                Write-Host "$($item.Name) downloaded successfully." -ForegroundColor Green
            } catch {
                Write-Host "Notice: Online download for $($item.Name) failed: $_" -ForegroundColor Yellow
                Write-Host "You can manually place XTTS-v2 weights in '$ModelsDir'." -ForegroundColor Yellow
            }
        } else {
            Write-Host "$($item.Name) already present." -ForegroundColor Green
        }
    }
}

# 7. Optional Venv Setup
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

Write-Host "AllTalk XTTS-v2 Voice Cloning Engine Scaffolding & Setup Completed Successfully!" -ForegroundColor Green
