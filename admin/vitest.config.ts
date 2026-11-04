/** Vitest config: happy-dom environment, `@` alias matching the app, tests under `tests/`. */
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@docs': fileURLToPath(new URL('../docs/.vitepress/theme/components', import.meta.url)),
      'vitepress': fileURLToPath(new URL('./tests/helpers/vitepress.ts', import.meta.url)),
    },
  },
  test: {
    environment: 'happy-dom',
    globals: true,
    include: ['tests/**/*.test.ts'],
    // Unmounts every mounted component after each test.
    setupFiles: ['tests/setup.ts'],
    exclude: ['**/node_modules/**', 'dist/**'],
  },
})
