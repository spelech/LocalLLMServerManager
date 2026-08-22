# Task 3 Brief: Refactor XAML Views & Modernize Control Layouts

## Objective
Modernize and streamline the control layouts across all tabs, replacing heavy layouts with compact telemetry chips, modern segmented tab styling, and clean card containers.

## Target Files
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/OllamaModelsTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/CivitaiTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`

## Requirements
1. **TelemetryHeaderControl.axaml**:
   - Streamline into horizontal compact telemetry chips/badges for CPU, RAM, GPU VRAM.
   - Use clean typography (`#f0f6fc` values, `#8b949e` labels), compact progress bars, and status indicator dots.
2. **MainView.axaml**:
   - Segmented tab bar with clean margins and `#0d1117` background (`BgDarkBrush`).
   - Clean status footer bar (`#161b22`, 1px `#30363d` top border).
3. **Tab Controls (Ollama, HuggingFace, Civitai, EngineStudio, Settings)**:
   - Ensure all card borders use `Classes="matte-card"` / `Classes="glass-card"`.
   - Ensure input text boxes use `Classes="matte-input"` / `Classes="glass-input"`.
   - Ensure buttons use `Classes="matte-primary"`, `Classes="matte-secondary"`, etc.
   - Clean spacing (padding="16", margin="0,0,0,12").

## Verification
- Run `dotnet build LocalLLMServerManager.sln` — must compile with 0 errors.
- Run `dotnet test` — all tests must pass.
- Commit with message: `feat(ui): streamline telemetry header, tab navigation, and card layouts`.
