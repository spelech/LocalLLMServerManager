### Task 5: Web Dashboard Auto-Discovery & Validation

**Files:**
- Modify: `docs/legacy-web-dash/index.html`
- Modify: `docs/legacy-web-dash/app.js`
- Run: `npm run lint` and `npx tsc --noEmit` from project root to verify

**Interfaces:**
- Consumes: `GET /api/system/tools/detect`, `POST /api/system/tools/apply-detected`, `POST /api/system/tools/validate`
- Produces:
  - An **"Auto-Detect Tools" card** in the Settings tab section of `index.html`:
    - A "🔍 Auto-Detect Installed Tools" button that calls `POST /api/system/tools/apply-detected` and refreshes the settings form fields.
    - Path validation status pills (🟢 Found / ⚠️ Missing / 🔍 Auto-Discovered) shown next to each path input.
  - In `app.js`: 
    - `autoDetectTools()` function: calls `POST /api/system/tools/apply-detected`, merges discovered paths into form fields for any that are empty, shows a toast notification.
    - `validatePaths()` function: calls `POST /api/system/tools/validate` with current form values, updates badge pill states.
    - Attach `autoDetectTools()` to the Auto-Detect button click.
    - Refresh path status badges when settings are loaded/saved.

**Steps:**
1. Examine the current `docs/legacy-web-dash/index.html` and `app.js` to understand the Settings tab layout and existing API call patterns.
2. Add the Auto-Detect card and validation status pill elements to `index.html`.
3. Implement `autoDetectTools()` and `validatePaths()` functions in `app.js`.
4. Run `npm run lint` and `npx tsc --noEmit` from the repo root to confirm no lint/type errors.
5. Commit: `git add docs/legacy-web-dash/ && git commit -m "feat: integrate tool auto-discovery and validation into web dashboard"`
