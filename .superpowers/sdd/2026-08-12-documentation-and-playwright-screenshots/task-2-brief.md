# Task 2 Brief: Modernize README.md & Embed Real Screenshots

## Requirements
1. Update `README.md` title and version badge to `v3.4.0` (Unified Avalonia XAML WebAssembly & Playwright E2E Release).
2. Embed the real Playwright PNG screenshots under their respective feature sections in `README.md`:
   - `![Dashboard Overview](docs/images/dashboard_desktop.png)`
   - `![Ollama Installed Models](docs/images/dashboard_ollama.png)`
   - `![Hugging Face GGUF Search](docs/images/dashboard_huggingface.png)`
   - `![CivitAI SD Checkpoints](docs/images/dashboard_civitai.png)`
   - `![3D Mesh & ComfyUI Studio](docs/images/dashboard_3d_studio.png)`
   - `![Application Settings](docs/images/dashboard_settings.png)`
3. Add a dedicated **Docker & Container Deployment** section documenting `Dockerfile` multi-stage build, `docker-compose.yml`, and `docker run -p 5246:5246`.
4. Add a dedicated **Playwright Automated E2E Browser Testing** section documenting `Microsoft.Playwright` browser integration and `dotnet test` commands.
5. Add a technical section on **WebAssembly Static Asset Hosting & Kestrel MIME Mappings** (`.dat`, `.symbols`, `.wasm` static file provider rules).
6. Update Versioning Convention table to include `v3.4.0`.

## Files
- Modify: `README.md`

## Verification Command
`git diff README.md`
