/**
 * Test runner configuration for the documentation site helpers (`.vitepress/*.ts`). Only the
 * files under `tests/` are collected, so the VitePress cache and the generated pages are never scanned.
 */
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    include: ['tests/**/*.test.ts'],
    environment: 'node',
  },
})
