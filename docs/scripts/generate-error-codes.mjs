/**
 * Builds the error code table of the Errors page from the backend sources, so the page cannot
 * drift from the code: the codes and their groups come from `HostpanelErrorCodes.cs` and
 * `SharedErrorCodes.cs`, the English sentence of each from `ErrorMessages.resx` (or
 * `ValidationMessages.resx` for the codes the shared edge answers with on its own) and the HTTP
 * status from `StatusMapping.cs`. See `lib/errorCatalog.mjs`.
 *
 * The output is `docs/api-reference/_partials/error-codes.md`, which `errors.md` includes. It is
 * not committed.
 *
 * Usage (from anywhere):
 *   node docs/scripts/generate-error-codes.mjs
 */

import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { loadErrorCatalog } from './lib/errorCatalog.mjs'

const outFile = resolve(dirname(fileURLToPath(import.meta.url)), '../api-reference/_partials/error-codes.md')

/**
 * Escapes a sentence for a Markdown table cell that VitePress also compiles as a Vue template.
 *
 * @param {string} text
 * @returns {string}
 */
const cell = (text) =>
  text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\{/g, '&#123;')
    .replace(/\}/g, '&#125;')
    .replace(/`/g, '&#96;')
    .replace(/\|/g, '&#124;')
    .replace(/\s*\r?\n\s*/g, ' ')

const catalog = loadErrorCatalog()

const lines = [
  `This table lists every error code the API can answer with: ${catalog.total} codes, read from the code at build time.`,
  '',
]
const render = (title, rows) => {
  lines.push(`### ${title}`, '', '| Code | Status | English sentence |', '|------|--------|------------------|')
  for (const { code, status, sentence } of rows) {
    lines.push(`| \`${code}\` | ${status} | ${cell(sentence)} |`)
  }
  lines.push('')
}

render('Shared codes', catalog.edge)
for (const [title, rows] of catalog.groups) { render(title, rows) }

mkdirSync(dirname(outFile), { recursive: true })
writeFileSync(outFile, lines.join('\n'))
console.log(`Wrote ${catalog.total} error codes in ${catalog.groups.size + 1} groups to ${outFile}`)
