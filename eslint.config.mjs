import js from '@eslint/js';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: [
      '**/bin/**',
      '**/obj/**',
      '**/dist/**',
      '**/publish/**',
      '**/node_modules/**',
      '**/wwwroot/**',
      '**/.superpowers/**',
      '**/screenshots/**',
      '**/scripts/**',
      '**/docs/**',
      '**/LocalLLMServerManager.Web/**',
      '**/test-results/**',
      '**/playwright-report/**',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
);
