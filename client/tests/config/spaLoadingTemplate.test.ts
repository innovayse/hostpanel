/**
 * Pins the loading shell of the browser-only routes (`/client/**`, `/cart/**`, `/checkout/**`): the
 * config names the file, and the file is an accessible status region that fills the viewport, follows
 * the colour scheme, stops its motion when the visitor asks for less, and never writes the mode class
 * on `<html>` (that has one writer, `plugins/color-mode.ts`).
 *
 * @module tests/config/spaLoadingTemplate.test
 */

import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The client root. */
const CLIENT_ROOT = resolve(import.meta.dirname, '..', '..')

/** The file name the config points at. */
const TEMPLATE_FILE = 'spa-loading-template.html'

/**
 * Reads a file of the client as text.
 *
 * @param name - Path relative to the client root.
 * @returns The contents.
 */
const read = (name: string): string => readFileSync(resolve(CLIENT_ROOT, name), 'utf8')

describe('spa loading template', () => {
  it('is the one the Nuxt config names', () => {
    expect(read('nuxt.config.ts')).toContain(`spaLoadingTemplate: '${TEMPLATE_FILE}'`)
  })

  it('is a busy status region, so a screen reader knows the page is loading', () => {
    const html = read(TEMPLATE_FILE)

    expect(html).toContain('role="status"')
    expect(html).toContain('aria-busy="true"')
    expect(html).toContain('aria-hidden="true"')
  })

  it('fills the viewport, so nothing moves when the app replaces it', () => {
    const html = read(TEMPLATE_FILE)

    expect(html).toMatch(/position:\s*fixed/)
    expect(html).toMatch(/inset:\s*0/)
  })

  it('takes the page colour from the colour scheme and stills its motion on request', () => {
    const html = read(TEMPLATE_FILE)

    expect(html).toContain('prefers-color-scheme: dark')
    expect(html).toContain('prefers-reduced-motion: reduce')
  })

  it('does not write the colour mode class itself', () => {
    const html = read(TEMPLATE_FILE)

    expect(html).not.toMatch(/classList|<script/)
  })
})
