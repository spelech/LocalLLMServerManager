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
