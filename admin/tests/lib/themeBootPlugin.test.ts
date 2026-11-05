/**
 * Tests for the theme pre-paint plugin (`src/lib/themeBootPlugin.ts`): the script reaches the page as
 * a same-origin file, never as an inline script the Content-Security-Policy would block.
 */
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it, vi } from 'vitest'
import { THEME_BOOT_FILE, themeBootPlugin } from '@/lib/themeBootPlugin'
import { themeBootScript } from '@/lib/themeBoot'

/** Shape of the hooks the plugin defines, narrowed from Vite's wider hook types for calling them directly. */
interface PluginHooks {
  /** Returns the tags to inject into the page. */
  transformIndexHtml: () => Array<{ tag: string, attrs?: Record<string, string>, children?: string, injectTo: string }>
  /** Emits the script into the build output. */
  generateBundle: (this: { emitFile: (file: unknown) => void }) => void
  /** Registers the dev server answer. */
  configureServer: (server: { middlewares: { use: (path: string, handler: (request: unknown, response: unknown) => void) => void } }) => void
}

/**
 * Builds the plugin and views its hooks as plain functions.
 *
 * @returns The plugin's hooks.
 */
const hooks = (): PluginHooks => themeBootPlugin() as unknown as PluginHooks

describe('themeBootPlugin', () => {
  it('is named so build logs show where the script comes from', () => {
    expect(themeBootPlugin().name).toBe('admin-theme-boot')
  })

  it('injects a blocking script that points at a relative file, with no inline body', () => {
    const tags = hooks().transformIndexHtml()

    expect(tags).toEqual([{ tag: 'script', attrs: { src: THEME_BOOT_FILE }, injectTo: 'head' }])
    expect(tags[0]?.children).toBeUndefined()
    // Relative, so it resolves against the <base href> the container rewrites, like config.js.
    expect(THEME_BOOT_FILE.startsWith('/')).toBe(false)
  })

  it('emits the pre-paint source as an asset of the build', () => {
    const emitFile = vi.fn()

    hooks().generateBundle.call({ emitFile })

    expect(emitFile).toHaveBeenCalledWith({ type: 'asset', fileName: THEME_BOOT_FILE, source: themeBootScript() })
  })

  it('answers the dev server request for the file with the same source', () => {
    const use = vi.fn()
    hooks().configureServer({ middlewares: { use } })
    const response = { setHeader: vi.fn(), end: vi.fn() }

    const [path, handler] = use.mock.calls[0] ?? []
    expect(path).toBe(`/${THEME_BOOT_FILE}`)
    handler?.({}, response)

    expect(response.setHeader).toHaveBeenCalledWith('Content-Type', 'text/javascript; charset=utf-8')
    expect(response.end).toHaveBeenCalledWith(themeBootScript())
  })
})

describe('index.html', () => {
  it('carries no inline script, which the Content-Security-Policy blocks', () => {
    const html = readFileSync(join(__dirname, '..', '..', 'index.html'), 'utf8').replace(/<!--[\s\S]*?-->/g, '')
    const scripts = [...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/g)]

    expect(scripts.length).toBeGreaterThan(0)
    for (const [, attributes, body] of scripts) {
      expect(attributes ?? '').toContain('src=')
      expect((body ?? '').trim()).toBe('')
    }
  })
})
