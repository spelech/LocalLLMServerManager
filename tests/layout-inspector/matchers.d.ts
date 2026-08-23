/* eslint-disable @typescript-eslint/no-unused-vars */
import '@playwright/test';

declare global {
  namespace PlaywrightTest {
    interface Matchers<R, T> {
      toHaveNoLayoutOverflow(): Promise<R>;
      toHaveMobileFit(): Promise<R>;
      toHaveTouchFriendlyTargets(options?: { minSize?: number }): Promise<R>;
    }
  }
}
