/**
 * `yarn lint:i18n`: checks the three locale trees against each other and against the source.
 *
 * Reports, per module file under `locales/<lang>/`:
 *  - missing: a key present in `en` but absent from `ru` or `hy`;
 *  - extra: a key present in `ru`/`hy` but absent from `en`;
 *  - unused: a key in `en` no source file references (a quoted literal of the full key, or a
 *    template literal whose static prefix covers it counts as a reference);
 *  - undefined: a literal `t('a.b')` call whose key is absent from `en`.
 *
 * Exits 1 when anything is reported, 0 otherwise.
 *
 * @module scripts/lint-i18n
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

/** The client root. */
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..')

/**
 * Locale directories, discovered from `locales/` so a new language needs no edit here (the
 * `AppLocale` enum cannot be imported from a plain `.mjs`; `tests/locales/parity.test.ts` pins
 * the folder set to the enum). `en` is the reference and goes first.
 */
const LOCALES = readdirSync(join(ROOT, 'locales'), { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name)
  .sort((a, b) => (a === 'en' ? -1 : b === 'en' ? 1 : a.localeCompare(b)))

/** Folders and files whose sources may reference keys. */
const SOURCE_ROOTS = ['components', 'pages', 'layouts', 'templates', 'composables', 'stores', 'plugins', 'middleware', 'utils', 'lib', 'server', 'types', 'constants', 'app.vue', 'error.vue']

/**
 * Flattens a message tree to dotted keys.
 *
 * @param {unknown} node A parsed JSON node.
 * @param {string} prefix Dotted path so far.
 * @param {Set<string>} out Accumulator.
 * @returns {Set<string>} The accumulator.
 */
const flatten = (node, prefix, out) => {
  if (node !== null && typeof node === 'object' && !Array.isArray(node)) {
    for (const [key, value] of Object.entries(node)) {
      flatten(value, prefix ? `${prefix}.${key}` : key, out)
    }
  } else {
    out.add(prefix)
  }
  return out
}

/**
 * Lists every file under a path whose name ends with one of the extensions.
 *
 * @param {string} path File or directory.
 * @param {string[]} extensions Wanted extensions.
 * @returns {string[]} Absolute file paths.
 */
const walk = (path, extensions) => {
  let stat
  try {
    stat = statSync(path)
  } catch {
    return []
  }
  if (stat.isFile()) {
    return extensions.some((ext) => path.endsWith(ext)) ? [path] : []
  }
  return readdirSync(path).flatMap((name) => (name === 'node_modules' || name.startsWith('.') ? [] : walk(join(path, name), extensions)))
}

/** Problems found, as printable lines. */
const problems = []

/** Per locale, the flat key set of every module file. */
const trees = Object.fromEntries(LOCALES.map((locale) => {
  const dir = join(ROOT, 'locales', locale)
  const modules = Object.fromEntries(
    readdirSync(dir).filter((name) => name.endsWith('.json')).map((name) => [name, flatten(JSON.parse(readFileSync(join(dir, name), 'utf8')), '', new Set())]),
  )
  return [locale, modules]
}))

// Module files a language does not translate yet; they fall back to English (see locales/untranslated.json).
const UNTRANSLATED = JSON.parse(readFileSync(join(ROOT, 'locales', 'untranslated.json'), 'utf8'))

for (const locale of LOCALES.filter((l) => l !== 'en')) {
  for (const file of new Set([...Object.keys(trees.en), ...Object.keys(trees[locale])])) {
    if ((UNTRANSLATED[locale] ?? []).includes(file)) {
      continue
    }
    const reference = trees.en[file] ?? new Set()
    const other = trees[locale][file] ?? new Set()
    for (const key of reference) {
      if (!other.has(key)) {
        problems.push(`missing  ${locale}/${file}: ${key}`)
      }
    }
    for (const key of other) {
      if (!reference.has(key)) {
        problems.push(`extra    ${locale}/${file}: ${key}`)
      }
    }
  }
}

/** All en keys, with the module file each lives in. */
const enKeys = new Map()
for (const [file, keys] of Object.entries(trees.en)) {
  for (const key of keys) {
    enKeys.set(key, file)
  }
}

const sources = SOURCE_ROOTS.flatMap((entry) => walk(join(ROOT, entry), ['.vue', '.ts', '.js', '.mjs']))
const text = sources.map((file) => readFileSync(file, 'utf8')).join('\n')

/** Static prefixes of template-literal keys, e.g. `status.` from `` `status.${code}` ``. */
const dynamicPrefixes = [...text.matchAll(/['"`]([\w.-]+\.)\$\{/g)].map((match) => match[1])

for (const key of enKeys.keys()) {
  const quoted = new RegExp(`['"\`]${key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}['"\`]`)
  if (!quoted.test(text) && !dynamicPrefixes.some((prefix) => key.startsWith(prefix))) {
    problems.push(`unused   en/${enKeys.get(key)}: ${key}`)
  }
}

for (const file of sources) {
  const content = readFileSync(file, 'utf8')
  for (const match of content.matchAll(/(?<![\w.])\$?t\(\s*['"`]([\w-]+(?:\.[\w-]+)+)['"`]\s*[,)]/g)) {
    if (!enKeys.has(match[1])) {
      problems.push(`undefined ${relative(ROOT, file)}: t('${match[1]}') is not in locales/en`)
    }
  }
}

if (problems.length > 0) {
  console.error(problems.join('\n'))
  console.error(`\nlint:i18n found ${problems.length} problem(s).`)
  process.exit(1)
}
console.log(`lint:i18n clean (${enKeys.size} keys, ${LOCALES.length} locales).`)
