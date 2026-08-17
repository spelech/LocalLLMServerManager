### Task 7: Documentation, Version Bump (v3.5.0), and Final Verification & Push

**Files:**
- Modify: `LocalLLMServerManager.csproj` (Version → 3.5.0)
- Modify: `LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj` (Version → 3.5.0 if present)
- Modify: `scripts/installer.iss` (version string → 3.5.0)
- Modify: `README.md` (add section on flexible path configuration & auto-discovery)
- Modify: `docs/REQUIREMENTS.md` (update with new configurable paths & discovery feature)
- Modify: `docs/DEVELOPMENT_GUIDE.md` (add section on tool discovery service, new endpoints, script parameterization)
- Modify: `docs/TEST_COVERAGE.md` (update with new test counts and coverage summary)

**Steps:**
1. **Bump version to 3.5.0**:
   - In `LocalLLMServerManager.csproj`: find `<Version>` or `<AssemblyVersion>` tag and set to `3.5.0`.
   - In `LocalLLMServerManager.Shared.csproj`: same if present.
   - In `scripts/installer.iss`: update `AppVersion` or `#define MyAppVersion`.

2. **Update README.md**:
   - Add a "Configuration & Auto-Discovery" section after the installation section explaining:
     - How to configure tool paths via settings.json (or the Settings UI)
     - How to use the "🔍 Auto-Detect Installed Tools" feature
     - Supported tool locations (Ollama, ComfyUI, SD Forge/A1111)
     - How helper scripts now accept `-ComfyUiPath`, `-ModelsDir`, etc.

3. **Update docs**:
   - `docs/REQUIREMENTS.md`: Add flexible path configuration and auto-discovery as a requirement.
   - `docs/DEVELOPMENT_GUIDE.md`: Document `IToolDiscoveryService`, new REST endpoints, and parameterized scripts.
   - `docs/TEST_COVERAGE.md`: Update test count and coverage details.

4. **Run full verification**:
   - `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj` (must pass 0 failures)
   - `npm run lint` (must pass)
   - `npx tsc --noEmit` (must pass)

5. **Commit**: `git add -A && git commit -m "docs: bump version to 3.5.0 and document flexible path configuration"`

6. **Push to remote**: `git push origin main` (or the current branch — check with `git branch --show-current` first)

**IMPORTANT**: Before pushing, verify the current branch name and push to the correct remote branch.
