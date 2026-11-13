/**
 * `npm run audit`: prints the conformance numbers of the admin against the frontend rules.
 *
 * Every count is a heuristic over the source text, good enough to watch the gap close; the lint
 * gates (`lint:ci`, `lint:i18n`) stay the source of truth. Every number should reach zero except
 * "vue files using t()", which should reach the total of vue files.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { countAny, countInputsWithoutPlaceholder, countStoresCallingUseApi, countVHtml, template } from './auditCounts.mjs'

/** Admin root: the folder above `scripts/`. */
const root = fileURLToPath(new URL('..', import.meta.url))

/** Source root. */
const src = join(root, 'src')

/**
 * Lists every `.vue` and `.ts` file under a directory.
 *
 * @param {string} dir - Directory to walk.
 * @returns {string[]} Absolute file paths.
 */
const walk = (dir) =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return walk(path)
    }
    return /\.(vue|ts)$/.test(name) ? [path] : []
  })

/** All source files with their text and kit membership. */
const files = walk(src).map((path) => ({
  path,
  rel: relative(src, path).split(sep).join('/'),
  text: readFileSync(path, 'utf8'),
}))

/** Vue files outside the kit. */
const screens = files.filter((f) => f.rel.endsWith('.vue') && !f.rel.startsWith('components/ui/'))

/** All Vue files. */
const vueFiles = files.filter((f) => f.rel.endsWith('.vue'))

/**
 * Counts regex matches across a file set.
 *
 * @param {{ text: string }[]} set - Files to scan.
 * @param {RegExp} pattern - Global pattern.
 * @returns {number} Total matches.
 */
const count = (set, pattern) => set.reduce((sum, f) => sum + (f.text.match(pattern)?.length ?? 0), 0)

/** Template text of every screen file. */
const screenTemplates = screens.map((f) => ({ text: template(f.text) }))

/** Input-like opening tags without a placeholder. */
const inputsWithoutPlaceholder = vueFiles.reduce((sum, f) => sum + countInputsWithoutPlaceholder(f.text), 0)

/** Bare text lines in templates: a line that is only words, not a tag, binding or comment. */
const bareText = screenTemplates.reduce(
  (sum, f) => sum + f.text.split('\n').filter((l) => /^\s*[A-Za-z][A-Za-z ,.'!?-]{2,}\s*$/.test(l)).length,
  0,
)

/** Const arrow functions without a JSDoc block directly above. */
const undocumentedArrows = files.reduce((sum, f) => {
  const lines = f.text.split('\n')
  return (
    sum +
    lines.filter(
      (line, i) =>
        /^\s*(export\s+)?const\s+\w+\s*(:[^=]+)?=\s*(async\s*)?(\([^)]*\)|\w+)\s*(:[^=]+)?=>/.test(line) &&
        !/\*\/\s*$/.test(lines[i - 1] ?? ''),
    ).length
  )
}, 0)

/** Stores that import the transport directly. */
const storesCallingUseApi = countStoresCallingUseApi(files.filter((f) => f.rel.includes('stores/')).map((f) => f.text))

/** The report, label to number. */
const report = {
  'vue files (total)': vueFiles.length,
  'raw elements outside components/ui': count(screenTemplates, /<(button|input|textarea|select|table|form|dialog|a)[\s>]/g),
  'function declarations': count(files, /^\s*(export\s+)?(async\s+)?function\s+\w+/gm),
  'style= attributes': count(vueFiles, /\sstyle="/g),
  '<style> blocks': count(vueFiles, /<style[\s>]/g),
  'v-html': countVHtml(vueFiles.map((f) => f.text)),
  'any': countAny(files.map((f) => f.text)),
  'inputs without placeholder': inputsWithoutPlaceholder,
  'vue files using t()': vueFiles.filter((f) => /(?<![\w$])\$?t\(/.test(f.text)).length,
  'bare text lines': bareText,
  'undocumented const arrows': undocumentedArrows,
  'stores calling useApi': storesCallingUseApi,
}

console.table(report)
