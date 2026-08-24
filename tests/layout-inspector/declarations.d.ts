declare global {
  namespace PlaywrightTest {
    interface Matchers<R> {
      toHaveNoLayoutOverflow(): R;
      toHaveMobileFit(): R;
      toHaveTouchFriendlyTargets(options?: { minSize?: number }): R;
    }
  }
}

export {};
