### Task 6: Parameterize & Modernize Helper Scripts

**Files:**
- Modify: `scripts/setup_ai_tools.ps1`
- Modify: `scripts/download_models.ps1`
- Modify: `scripts/download_media_models.ps1`
- Modify: `scripts/install_comfy_nodes.ps1`
- Modify: `scripts/fix_comfy.ps1`
- Modify: `scripts/download_all.py`
- Modify: `scripts/hf_download.py`

**Interfaces:**
- Consumes: `settings.json` (path: same directory as script, or passed via param) and CLI arguments.
- Produces: Fully parameterized scripts that do not hardcode `D:\AI`, `C:\AI`, or any other fixed paths.

**Rules:**
- Scripts must NOT hardcode any path assumptions like `D:\AI`, `C:\AI`, `C:\SD_Forge`, etc.
- Default values should be empty strings or discovered dynamically from `settings.json`.
- Each PowerShell script must accept CLI parameters with `[CmdletBinding()]` and `param()` blocks.
- Each Python script must use `argparse` for all path arguments.

**Steps for each PowerShell script (`setup_ai_tools.ps1`, `download_models.ps1`, `download_media_models.ps1`, `install_comfy_nodes.ps1`, `fix_comfy.ps1`):**
1. Examine current hardcoded paths in the script.
2. Add a `param()` block at top with parameters like `-SettingsJson`, `-ComfyUiPath`, `-ModelsDir`, `-ForgeDir`, etc.
3. Add logic to read `settings.json` if it exists and no parameter was passed (use `ConvertFrom-Json`).
4. Replace all hardcoded path strings with the parameterized equivalents.
5. If a required path cannot be resolved from params or settings, write a clear error message and exit gracefully (do not fail silently).

**Steps for Python scripts (`download_all.py`, `hf_download.py`):**
1. Add `argparse` at the top.
2. Replace all hardcoded directory strings with argparse arguments that default to `None` (not a hardcoded path).
3. If a required path is `None`, print a helpful usage message and exit.

**Commit:** `git add scripts/ && git commit -m "feat: parameterize helper scripts to read settings.json and accept CLI arguments"`
