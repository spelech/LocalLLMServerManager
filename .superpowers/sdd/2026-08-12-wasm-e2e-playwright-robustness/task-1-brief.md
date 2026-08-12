# Task 1 Brief: Configure Kestrel Static File Mime Types & WASM Build Sync

## Requirements
1. Update `Program.cs` to configure `Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider` for static files served from `wwwroot/`.
2. Add explicit MIME mappings:
   - `.dat` -> `application/octet-stream`
   - `.symbols` -> `application/octet-stream`
   - `.wasm` -> `application/wasm`
   - `.clr` -> `application/octet-stream`
   - `.pdb` -> `application/octet-stream`
   - `.boot.json` -> `application/json`
3. Configure `app.UseStaticFiles()` with `ServeUnknownFileTypes = true` and `DefaultContentType = "application/octet-stream"`.
4. Synchronize all `AppBundle` files (`icudt_EFIGS.dat`, `dotnet.native.js.symbols`, `icudt_CJK.dat`, `icudt_no_CJK.dat`) from `LocalLLMServerManager.Web/bin/Release/net10.0/browser-wasm/AppBundle/_framework/*` into `wwwroot/_framework/`.
5. Create unit test `LocalLLMServerManager.Tests/StaticFileMimeTypeTests.cs` verifying that requests to `/_framework/icudt_EFIGS.dat` and `/_framework/dotnet.native.js.symbols` return HTTP 200 OK with `application/octet-stream` Content-Type.

## Files
- Modify: `Program.cs`
- Create: `LocalLLMServerManager.Tests/StaticFileMimeTypeTests.cs`
- Sync assets: `wwwroot/_framework/`

## Verification Command
`dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StaticFileMimeTypeTests" -c Release`
