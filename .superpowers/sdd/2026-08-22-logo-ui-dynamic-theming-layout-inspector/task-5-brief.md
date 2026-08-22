# Task 5 Brief: Setup Node/TS Tooling, Playwright, and spelech/playwright-layout-inspector

## Objective
Set up the Node.js / TypeScript environment with ESLint, TypeScript, Playwright, and `@spelech/playwright-layout-inspector` (via `github:spelech/playwright-layout-inspector`), configure `playwright.config.ts`, and write the layout audit spec for the WASM dashboard.

## Target Files
- Create: `package.json`
- Create: `tsconfig.json`
- Create: `eslint.config.mjs`
- Create: `playwright.config.ts`
- Create: `tests/layout-inspector/layout-audit.spec.ts`

## Requirements

### 1. package.json
```json
{
  "name": "localllm-server-manager-tooling",
  "version": "1.0.0",
  "private": true,
  "type": "module",
  "scripts": {
    "lint": "eslint .",
    "typecheck": "tsc --noEmit",
    "test:layout": "playwright test --config=playwright.config.ts"
  },
  "devDependencies": {
    "@eslint/js": "^9.20.0",
    "@playwright/test": "^1.50.0",
    "@types/node": "^22.0.0",
    "eslint": "^9.20.0",
    "playwright-layout-inspector": "github:spelech/playwright-layout-inspector",
    "typescript": "^5.7.0",
    "typescript-eslint": "^8.24.0"
  }
}
```

### 2. tsconfig.json & eslint.config.mjs
- `tsconfig.json`: Target ES2022 / NodeNext or Bundler, strict mode, noEmit: true.
- `eslint.config.mjs`: ESLint flat config using `@eslint/js` and `typescript-eslint`.

### 3. playwright.config.ts
- Test Directory: `./tests/layout-inspector`
- Base URL: `process.env.TEST_BASE_URL || 'http://localhost:5000'`
- Projects:
  - `Desktop 1080p`: 1920x1080
  - `Desktop 1440p`: 2560x1440
  - `Tablet iPad`: iPad Pro 11 preset
  - `Mobile Galaxy S25+`: 412x915 viewport with mobile device emulation

### 4. tests/layout-inspector/layout-audit.spec.ts
```typescript
import { test, expect } from '@playwright/test';
import 'playwright-layout-inspector/matchers';

test.describe('L³M² Web Dashboard Layout & UX Audit', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('#out', { timeout: 15000 });
  });

  test('assert zero horizontal viewport overflow and canvas bleeding', async ({ page }) => {
    await expect(page).toHaveNoLayoutOverflow();
  });

  test('assert viewport and mobile fit standards', async ({ page }) => {
    await expect(page).toHaveMobileFit();
  });

  test('assert interactive touch targets meet ergonomics standards', async ({ page }) => {
    await expect(page).toHaveTouchFriendlyTargets({ minSize: 24 });
  });
});
```

## Verification
- Run `npm install`
- Run `npm run lint` — must pass with 0 errors.
- Run `npx tsc --noEmit` — must pass with 0 errors.
- Commit with message: `feat(tooling): integrate playwright-layout-inspector audit suite for WASM dashboard`.
