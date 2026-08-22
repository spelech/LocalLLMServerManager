import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/layout-inspector',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'list',
  use: {
    baseURL: process.env.TEST_BASE_URL || 'http://localhost:5000',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'Desktop 1080p',
      use: {
        viewport: { width: 1920, height: 1080 },
      },
    },
    {
      name: 'Desktop 1440p',
      use: {
        viewport: { width: 2560, height: 1440 },
      },
    },
    {
      name: 'Tablet iPad',
      use: {
        ...devices['iPad Pro 11'],
      },
    },
    {
      name: 'Mobile Galaxy S25+',
      use: {
        viewport: { width: 412, height: 915 },
        deviceScaleFactor: 2.625,
        isMobile: true,
        hasTouch: true,
        defaultBrowserType: 'chromium',
      },
    },
  ],
});
