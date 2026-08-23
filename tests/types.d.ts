import '@playwright/test';

declare global {
  namespace PlaywrightTest {
    interface Matchers<R> {
      toHaveNoLayoutOverflow(): Promise<R>;
      toHaveMobileFit(): Promise<R>;
      toHaveTouchFriendlyTargets(options?: { minSize?: number }): Promise<R>;
    }
  }
}
