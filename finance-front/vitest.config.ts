import { defineConfig, mergeConfig } from 'vitest/config';
import viteConfig from './vite.config';

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      css: false,
      restoreMocks: true,
      testTimeout: 15000,
      env: {
        VITE_API_URL: 'http://localhost:8080/api',
      },
      coverage: {
        provider: 'v8',
        include: ['src/**/*.{ts,tsx}'],
        exclude: [
          'src/shared/components/ui/**',
          'src/test/**',
          'src/main.tsx',
          'src/**/*.test.{ts,tsx}',
          'src/**/*.d.ts',
        ],
        thresholds: {
          lines: 80,
          statements: 80,
          functions: 80,
          branches: 75,
        },
      },
    },
  })
);
