/**
 * Layering gate, run by `yarn lint`.
 *
 * Nuxt auto-imports hide `useApi()` and `use<Area>Api()` from import-based lint rules, so the
 * layer rule from `rules/frontend.md` is also enforced by reading the source:
 *  - a `.vue` file never calls `useApi`, `apiFetch`, `$fetch`, `useFetch`, an api composable, the
 *    error-sentence helper, and holds no `catch`;
 *  - a store never uses `useApi` or `$fetch`;
 *  - no source file spells a client-area path (`'/client/<page>'`) as a string literal: paths come
 *    from `constants/clientRoutes.ts` (constants and the `client*Path` helpers).
 *
 * Every `.vue` file and every store of the client is checked (tests and e2e are not source).
 * Exits 1 with one line per hit.
 *
 * @module scripts/check-layers
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

/** The client root. */
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..')

/** What a `.vue` file may not contain. */
const VUE_FORBIDDEN = [
  { pattern: /\b(?:useApi|apiFetch|useFetch)\s*\(/, what: 'transport call' },
  { pattern: /\$fetch\s*\(/, what: '$fetch call' },
  { pattern: /\buse[A-Z]\w*Api\s*\(/, what: 'api composable call' },
  { pattern: /\b(?:apiErrorMessage|apiFieldErrors|apiErrorCode)\b/, what: 'error helper' },
  { pattern: /\bcatch\b\s*(?:\(|\{)/, what: 'catch block' },
]

/** What a store may not contain. */
const STORE_FORBIDDEN = [
  { pattern: /\buseApi\s*\(/, what: 'useApi call' },
  { pattern: /\$fetch\s*\(/, what: '$fetch call' },
]

/** A string literal that starts a client-area path: `'/client`, `"/client` or `` `/client ``. */
const CLIENT_PATH_LITERAL = /['"`]\/client(?:\/|['"`?])/

/**
 * The files allowed to spell client paths: the route table itself, the route-rule glob in the
 * config, and the backend's `/api/client/plugins` endpoints, which share the prefix by name only.
 */
const CLIENT_PATH_ALLOWED = new Set(['constants/clientRoutes.ts', 'nuxt.config.ts', 'server/utils/pluginBackendPaths.ts'])

/**
 * Whether a line is only a comment, where paths are prose rather than code.
 *
 * @param {string} line Source line.
 * @returns {boolean} True for a JSDoc, block or line comment, or an HTML comment.
 */
const isCommentLine = (line) => /^(?:\*|\/\/|\/\*|<!--)/.test(line.trim())

/**
 * Lists source files under a directory.
 *
 * @param {string} dir Directory.
 * @returns {string[]} Absolute paths of `.vue`/`.ts` files.
 */
const walk = (dir) => {
  let entries
  try {
    entries = readdirSync(dir)
  } catch {
    return []
  }
  return entries.flatMap((name) => {
    if (name === 'node_modules' || name.startsWith('.') || name === 'tests' || name === 'e2e') {
      return []
    }
    const path = join(dir, name)
    return statSync(path).isDirectory() ? walk(path) : /\.(vue|ts)$/.test(name) ? [path] : []
  })
}

/** Hits as printable lines. */
const hits = []

for (const file of walk(ROOT)) {
  const rel = relative(ROOT, file).replaceAll('\\', '/')
  const rules = rel.endsWith('.vue') ? VUE_FORBIDDEN : rel.startsWith('stores/') ? STORE_FORBIDDEN : []
  const lines = readFileSync(file, 'utf8').split('\n')
  lines.forEach((line, index) => {
    for (const { pattern, what } of rules) {
      if (pattern.test(line)) {
        hits.push(`${rel}:${index + 1}  ${what}: ${line.trim()}`)
      }
    }
    if (!CLIENT_PATH_ALLOWED.has(rel) && !isCommentLine(line) && CLIENT_PATH_LITERAL.test(line)) {
      hits.push(`${rel}:${index + 1}  client path literal (use constants/clientRoutes): ${line.trim()}`)
    }
  })
}

if (hits.length > 0) {
  console.error(hits.join('\n'))
  console.error(`\ncheck-layers found ${hits.length} layering violation(s).`)
  process.exit(1)
}
console.log('check-layers clean.')
