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
      'wwwroot/**',
      '**/wwwroot/**',
      'wwwroot_wasm/**',
      '**/wwwroot_wasm/**',
      'wwwroot/_framework/**',
      '**/wwwroot/_framework/**',
      '**/.superpowers/**',
      '**/screenshots/**',
      '**/scripts/**',
      '**/docs/**',
      'LocalLLMServerManager.Web/**',
      '**/LocalLLMServerManager.Web/**',
      '**/test-results/**',
      '**/playwright-report/**',
      '**/.dotnet/**',
      '**/C:*/**',
      '**/C*/**',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
);
