/**
 * The Vite plugin that serves the theme pre-paint script as a file of its own.
 *
 * The panel's Content-Security-Policy allows scripts from the same origin only, so the script that
 * applies the stored light/dark choice cannot sit inline in `index.html` (the browser blocks it on
 * every load). The plugin writes the same source text {@link themeBootScript} produces into
 * `theme-boot.js` (an emitted asset in a build, a middleware answer under the dev server) and puts a
 * classic, blocking `<script src>` at the end of `<head>`, so the choice is still on `<html>` before
 * the first paint.
 */
import type { Plugin } from 'vite'
// Relative and with its extension: Node loads this file for vite.config.ts and knows neither the `@/` alias nor an extensionless import.
import { themeBootScript } from './themeBoot.ts'

/**
 * File name of the pre-paint script, relative so it resolves against the `<base href>` the container
 * rewrites at start, exactly like `config.js`.
 */
export const THEME_BOOT_FILE = 'theme-boot.js'

/**
 * Builds the plugin.
 *
 * @returns The Vite plugin named `admin-theme-boot`.
 */
export const themeBootPlugin = (): Plugin => ({
  name: 'admin-theme-boot',
  /**
   * Puts the blocking script tag at the end of `<head>`, with no inline body.
   *
   * @returns The one tag to inject.
   */
  transformIndexHtml: () => [{ tag: 'script', attrs: { src: THEME_BOOT_FILE }, injectTo: 'head' }],
  /** Writes the script into the build output next to `config.js`, under the name the tag asks for. */
  generateBundle() {
    this.emitFile({ type: 'asset', fileName: THEME_BOOT_FILE, source: themeBootScript() })
  },
  /**
   * Answers the script request under the dev server, where nothing is emitted.
   *
   * @param server - The dev server whose middleware chain gets the answer.
   */
  configureServer(server) {
    server.middlewares.use(`/${THEME_BOOT_FILE}`, (_request, response) => {
      response.setHeader('Content-Type', 'text/javascript; charset=utf-8')
      response.setHeader('Cache-Control', 'no-cache')
      response.end(themeBootScript())
    })
  },
})
