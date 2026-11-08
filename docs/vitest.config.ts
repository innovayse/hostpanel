/**
 * Test runner configuration for the documentation site helpers (`.vitepress/*.ts`) and the theme
 * components. Only the files under `tests/` are collected, so the VitePress cache and the generated pages
 * are never scanned. Single file components are compiled by the Vue plugin; a test that needs a DOM asks
 * for it with a `@vitest-environment` comment, every other test runs in plain Node.
 */
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [vue()],
  test: {
    include: ['tests/**/*.test.ts'],
    environment: 'node',
  },
})
