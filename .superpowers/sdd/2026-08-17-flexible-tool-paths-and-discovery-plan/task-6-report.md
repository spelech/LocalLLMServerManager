# Task 6 Report: Parameterize & Modernize Helper Scripts

## Status
**DONE**

## Commits Created
- `9dc37cf`: `feat: parameterize helper scripts to read settings.json and accept CLI arguments`

## One-line Summary
Eliminated all hardcoded path assumptions across PowerShell and Python helper scripts, equipping them with CLI arguments, automatic `settings.json` discovery/resolution, and dynamic configuration generation.

---

## Detailed Implementation Summary

### 1. `scripts/setup_ai_tools.ps1`
- Added `[CmdletBinding()]` and `param()` block supporting:
  - `-TargetDir`: Base directory for tool installations (e.g. `$HOME\AI` or configured path).
  - `-ModelsDir`: Base models directory for shared weights.
  - `-SettingsJson`: Custom path to `settings.json`.
  - `-SevenZipPath`: Custom path to 7-Zip executable.
  - `-Interactive`: Switch to prompt user if paths are not supplied.
- Added dynamic `settings.json` discovery (`Get-AppSettings`) searching passed path, `$PSScriptRoot`, parent directory, and `%APPDATA%\LocalLLMServerManager\settings.json`.
- Added automatic environment variable expansion (`Resolve-PathVariables`) for `%APPDATA%`, `%USERPROFILE%`, etc.
- Added auto-discovery for 7-Zip via `Get-Command 7z` and standard Program Files paths with clean error messaging.
- Dynamically generates `extra_model_paths.yaml` in ComfyUI with the resolved `$ModelsDir`.
- Dynamically configures ComfyUI-Manager in the resolved custom nodes path.

### 2. `scripts/download_models.ps1`
- Added `[CmdletBinding()]` and `param()` block supporting:
  - `-ModelsDir`: Target directory for model downloads.
  - `-SettingsJson`: Custom path to `settings.json`.
  - `-Interactive`: Switch to prompt user interactively.
- Resolves checkpoints directory under `$ModelsDir\checkpoints`.
- Reads `ForgeModelsPath` and `ComfyModelsPath` from `settings.json` when `-ModelsDir` is omitted.
- Checks if model files already exist before downloading.

### 3. `scripts/download_media_models.ps1`
- Added `[CmdletBinding()]` and `param()` block supporting:
  - `-ModelsDir`: Target directory for video/media models.
  - `-SettingsJson`: Custom path to `settings.json`.
  - `-HfToken`: Explicit Hugging Face auth token.
  - `-Interactive`: Switch for interactive prompt.
- Resolves `checkpoints` and `animatediff_models` directories dynamically.
- Resolves Hugging Face authentication token from CLI, `$env:HF_TOKEN`, or local `.env` files.
- Downloads AnimateDiff SDXL motion module and Stable Video Diffusion XT 1.1 with optional Bearer auth.

### 4. `scripts/install_comfy_nodes.ps1`
- Added `[CmdletBinding()]` and `param()` block supporting:
  - `-ComfyUiPath`: Path to ComfyUI installation or `custom_nodes` folder.
  - `-SettingsJson`: Custom path to `settings.json`.
  - `-Interactive`: Switch for interactive prompt.
- Auto-resolves custom nodes directory across portable and standard layouts (`ComfyUI\custom_nodes`, `custom_nodes`).
- Uses `Push-Location` / `Pop-Location` to safely clone and update 3D and Video nodes without leaking working directory changes.

### 5. `scripts/fix_comfy.ps1`
- Added `[CmdletBinding()]` and `param()` block supporting `-TargetDir`, `-ModelsDir`, `-ComfyUiPath`, `-SettingsJson`, and `-Interactive`.
- Renames legacy portable folders dynamically within `$TargetDir`.
- Dynamically generates `extra_model_paths.yaml` with the resolved `$ModelsDir`.
- Ensures ComfyUI-Manager is installed.
- Calls `install_comfy_nodes.ps1` with the resolved ComfyUI path and settings.

### 6. `scripts/download_all.py`
- Refactored using Python `argparse`:
  - `--models-dir` (`-m`), `--output-dir` (`-o`), `--settings-json` (`-s`), `--token` (`-t`).
- Reads `settings.json` (`ComfyModelsPath`, `ForgeModelsPath`) and resolves environment variables.
- Falls back to `~/AI/models` if no arguments or settings are provided.
- Streams chunked download with Bearer auth support and existence checks.

### 7. `scripts/hf_download.py` & `scripts/hf_download_juggernaut.py`
- Refactored using Python `argparse`:
  - `--checkpoints-dir` (`-c`), `--models-dir` (`-m`), `--settings-json` (`-s`), `--token` (`-t`).
- Resolves checkpoints directory dynamically from CLI, base models dir, or `settings.json`.
- Passes Hugging Face tokens and handles alternative fallback model filenames.

---

## Verification
- Validated PowerShell syntax across all `.ps1` files in `scripts/` using PowerShell AST parser: **All passed (0 errors)**.
- Validated Python syntax using `py_compile`: **All passed (0 errors)**.
- Verified CLI `--help` output for `download_all.py`, `hf_download.py`, and `hf_download_juggernaut.py`.
- Verified PowerShell parameter definitions and bindings via `Get-Command`.
- Verified no hardcoded `D:\AI` or `C:\AI` strings remain in `scripts/`.
- Committed changes: `9dc37cf`
