# Task 1 Brief: Generate and Install Monochromatic L³M² Brand Assets

## Objective
Install the approved official monochromatic L³M² brand icon and logo assets across Desktop, System Tray, and WASM web dashboard.

## Target Files
- Create: `Assets/logo.svg`
- Modify: `Assets/app-icon.ico`
- Modify: `Assets/app-icon.png`
- Modify: `Assets/app_tray_icon.jpg`
- Modify: `LocalLLMServerManager.Web/wwwroot/favicon.ico`
- Modify: `LocalLLMServerManager.Web/wwwroot/index.html`

## Exact SVG Content (`Assets/logo.svg`)
```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="100%" height="100%">
  <rect width="128" height="128" rx="28" fill="#14171d"/>
  <text x="24" y="86" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif" font-weight="900" font-size="54" fill="#ffffff">L</text>
  <text x="52" y="56" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif" font-weight="800" font-size="25" fill="#ffffff">3</text>
  <text x="64" y="86" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif" font-weight="900" font-size="54" fill="#ffffff">M</text>
  <text x="104" y="56" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif" font-weight="800" font-size="25" fill="#ffffff">2</text>
</svg>
```

## Steps
1. Create `Assets/logo.svg` with the exact vector markup above.
2. Render PNG/ICO icons using node/canvas or C#/SkiaSharp or PowerShell/.NET Drawing to generate multi-resolution ICO/PNG/JPEG assets:
   - `Assets/app-icon.ico` (multi-size ICO containing 256, 128, 64, 48, 32, 24, 16)
   - `Assets/app-icon.png` (512x512 PNG)
   - `Assets/app_tray_icon.jpg` (128x128 JPEG)
   - `LocalLLMServerManager.Web/wwwroot/favicon.ico`
3. Update `LocalLLMServerManager.Web/wwwroot/index.html` title to "L³M² — Local LLM Server Manager" and favicon link.
4. Run `dotnet test --filter "FullyQualifiedName~StaticFileMimeTypeTests"` to verify test passes.
5. Commit changes with message `feat(branding): install monochromatic L³M² icon and logo assets`.
