/**
 * Legacy error-field gate, run by `yarn lint`.
 *
 * The C# API's refusal body is problem+json `{ type, title, status, detail, errors }`. An
 * `{ error, code }` pair is not on the wire, and a client that read it would silently render
 * nothing. This gate fails when source under `client/` reads `.error` or `.code` off a
 * response body (`data.error`, `err.data.error`, `body.code`, `err.data.code` and the like).
 *
 * A read is allowed only on a line carrying the marker `legacy-gate: allow <reason>` (comment on
 * the same line or the line above). One exists by design:
 *  - the BFF envelope's `data.code` in `lib/errorHandler.ts`: that `code` is our own field, set by
 *    `server/utils/api.ts`, not a backend field.
 *
 * Tests, scripts and generated folders are not scanned. Exits 1 with one line per hit.
 *
 * @module scripts/check-no-legacy-error-fields
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

/** The client root. */
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..')

/** Folders that are not product source. */
const SKIP = new Set(['node_modules', '.nuxt', '.output', '.git', 'tests', 'e2e', 'scripts', 'locales', 'public'])

/** Reads of `error` / `code` off something that holds a response body. */
const LEGACY_READ = /(?:\b(?:data|body|response|res|err|error|e|fetchErr|payload)\??\.(?:data\??\.)?(?:error|code)\b)/

/** The marker that allows a read, on the same line or the one above. */
const ALLOW = /legacy-gate:\s*allow/

/**
 * Lists the `.ts` / `.vue` / `.mjs` files under a directory.
 *
 * @param {string} dir Directory.
 * @returns {string[]} Absolute paths.
 */
const walk = (dir) => {
  let names
  try {
    names = readdirSync(dir)
  } catch {
    return []
  }
  return names.flatMap((name) => {
    if (SKIP.has(name)) {
      return []
    }
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return walk(path)
    }
    return /\.(ts|vue|mjs)$/.test(name) && !name.endsWith('.config.mjs') ? [path] : []
  })
}

/**
 * Strips a trailing line comment and a leading block-comment star so prose that mentions
 * `data.error` does not count as a read.
 *
 * @param {string} line One source line.
 * @returns {string} The code part.
 */
const codeOf = (line) => {
  const trimmed = line.trim()
  if (trimmed.startsWith('*') || trimmed.startsWith('//') || trimmed.startsWith('/*')) {
    return ''
  }
  return line.replace(/\s\/\/.*$/, '')
}

/** Hits as printable lines. */
const hits = []

for (const file of walk(ROOT)) {
  const lines = readFileSync(file, 'utf8').split('\n')
  lines.forEach((line, index) => {
    if (LEGACY_READ.test(codeOf(line)) && !ALLOW.test(line) && !ALLOW.test(lines[index - 1] ?? '')) {
      hits.push(`${relative(ROOT, file).replaceAll('\\', '/')}:${index + 1}  ${line.trim()}`)
    }
  })
}

if (hits.length > 0) {
  console.error(hits.join('\n'))
  console.error(`\ncheck-no-legacy-error-fields found ${hits.length} legacy error-field read(s).`)
  process.exit(1)
}
console.log('check-no-legacy-error-fields clean.')
