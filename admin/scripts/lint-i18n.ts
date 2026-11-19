/**
 * `npm run lint:i18n`: fails the build when the catalogues drift.
 *
 * Loads `src/locales/{en,ru,hy}/<area>.json` (every key prefixed with the file name, the area),
 * then reports keys missing in a language, extra keys, unused keys and `t('literal')` calls whose key
 * is absent from `en`; keys written as data in the screen schemas and the key tables (see `declaredKeys`) count as used and
 * must exist in `en`. The comparison itself lives in `scripts/lib/i18nCheck.ts`.
 */
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { basename, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { checkI18n, declaredKeys, flattenKeys, LANGUAGES, type Catalogues, type Language, type SourceFile } from './lib/i18nCheck'

/** Admin root: the folder above `scripts/`. */
const root = fileURLToPath(new URL('..', import.meta.url))

/** The locale folder. */
const localesDir = join(root, 'src', 'locales')

/**
 * Reads the flat keys of one language from its per-area folder.
 *
 * @param language - The language to load.
 * @returns The language's dotted keys.
 */
const loadLanguage = (language: Language): Set<string> => {
  const keys = new Set<string>()
  const folder = join(localesDir, language)
  if (existsSync(folder)) {
    for (const file of readdirSync(folder).filter((f) => f.endsWith('.json'))) {
      const area = basename(file, '.json')
      flattenKeys(JSON.parse(readFileSync(join(folder, file), 'utf8')), area).forEach((k) => keys.add(k))
    }
  }
  return keys
}

/**
 * Lists every `.vue` and `.ts` file under a directory.
 *
 * @param dir - Directory to walk.
 * @param base - The directory the paths are given relative to.
 * @returns Each file with its path under `base` (forward slashes) and its text.
 */
const readSources = (dir: string, base: string): SourceFile[] =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return readSources(path, base)
    }
    return /\.(vue|ts)$/.test(name) ? [{ path: relative(base, path).replaceAll('\\', '/'), text: readFileSync(path, 'utf8') }] : []
  })

const catalogues = Object.fromEntries(LANGUAGES.map((l) => [l, loadLanguage(l)])) as Catalogues
const sources = readSources(join(root, 'src'), join(root, 'src'))
const problems = checkI18n({ catalogues, source: sources.map((file) => file.text).join('\n'), declared: declaredKeys(sources) })

if (problems.length > 0) {
  console.error(problems.join('\n'))
  console.error(`\nlint:i18n failed: ${problems.length} problem(s).`)
  process.exit(1)
}
console.log('lint:i18n passed.')
