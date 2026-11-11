/**
 * Placeholder pages for the languages of the documentation. Every English page that has no translation
 * yet gets a generated page in each other language: a short notice in that language followed by the
 * English text. The reader of `/ru/staff/orders` therefore never meets a 404 while the translation is
 * still being written, and a translator adds a page by creating the file, nothing else.
 *
 * Every language, English included, keeps its pages in a folder of its own (`docs/en`, `docs/ru`, `docs/hy`);
 * the site config serves English at the root and the others below their folder with VitePress `rewrites`.
 * The placeholders are written to `docs/_fallback/<language>/...`, which is ignored by git, and the same
 * `rewrites` map them back to `<language>/...`. Nothing is written into the
 * folders translators work in, so a placeholder can never be committed by mistake or hide a real page.
 * The functions are pure where they can be (they take the text, not the file) so the tests run without
 * a build.
 */
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, posix, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

/** Folder of the language files and `locales.json`. */
export const I18N_DIR = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '.vitepress', 'i18n')

/**
 * Settings of the languages, as `locales.json` holds them.
 * @typedef {object} LocaleSettings
 * @property {string} default Key of the default language.
 * @property {string} fallbackDir Folder the placeholders are written to.
 * @property {Record<string, { prefix: string, folder: string }>} locales Each language by key.
 * @property {string[]} untranslated Paths (below a language's source folder) that are never translated; folders end with a slash.
 */

/**
 * Reads `locales.json`.
 * @returns {LocaleSettings} The settings.
 */
export const readSettings = () => JSON.parse(readFileSync(join(I18N_DIR, 'locales.json'), 'utf8'))

/**
 * Reads the notice texts of a language.
 * @param {string} locale Language key.
 * @returns {{ title: string, message: string, original: string }} The `fallback` block of its language file.
 */
export const readFallbackText = (locale) => JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')).fallback

/** Folders that never hold pages. */
const SKIPPED_DIRECTORIES = new Set(['node_modules', 'public', 'dist', 'cache'])

/**
 * Lists the markdown files below a folder.
 * @param {string} dir Folder to walk.
 * @param {string} [prefix] Path of the folder below the root, used while recursing.
 * @returns {string[]} Paths relative to the first folder, with `/` separators, sorted. Hidden folders, folders that
 * start with an underscore and folders that never hold pages are skipped.
 */
export const listMarkdown = (dir, prefix = '') => {
  if (!existsSync(dir)) {
    return []
  }
  const found = []
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = prefix ? `${prefix}/${entry.name}` : entry.name
    if (entry.isDirectory()) {
      if (!entry.name.startsWith('.') && !entry.name.startsWith('_') && !SKIPPED_DIRECTORIES.has(entry.name)) {
        found.push(...listMarkdown(join(dir, entry.name), path))
      }
    } else if (entry.name.endsWith('.md')) {
      found.push(path)
    }
  }
  return found.sort()
}

/**
 * Whether a page is on the list of pages that are not translated.
 * @param {string} path Source path in the English tree, such as `brand/README.md`.
 * @param {string[]} untranslated The list from `locales.json`.
 * @returns {boolean} True for a listed page, or a page below a listed folder.
 */
export const isUntranslated = (path, untranslated) => {
  const clean = path.replace(/^\/+/, '')
  return untranslated.some((entry) =>
    entry.endsWith('/') ? clean.startsWith(entry) || `${clean}/` === entry : clean === entry || clean === entry.replace(/\.md$/, ''),
  )
}

/**
 * Lists the pages of the English tree that can be translated.
 * @param {string} docsRoot The docs folder.
 * @param {LocaleSettings} [settings] The language settings; read from `locales.json` when omitted.
 * @returns {string[]} Source paths below the English folder, such as `staff/orders.md`.
 */
export const listEnglishPages = (docsRoot, settings = readSettings()) => {
  return listMarkdown(join(docsRoot, settings.locales[settings.default].folder)).filter((path) => !isUntranslated(path, settings.untranslated))
}

/**
 * Folder of the English sources below the docs root.
 * @returns {string} `en`.
 */
export const englishFolder = () => {
  const settings = readSettings()
  return settings.locales[settings.default].folder
}

/**
 * The address key of a source path: the address without a leading slash and without the extension, with `index` removed.
 * @param {string} path Source path such as `staff/index.md` or `staff/orders.md`.
 * @returns {string} `staff/` or `staff/orders`; an empty string for the home page.
 */
export const pageKey = (path) => path.replace(/(^|\/)index\.md$/, '$1').replace(/\.md$/, '')

/**
 * Lists the pages a language really has (not placeholders).
 * @param {string} docsRoot The docs folder.
 * @param {string} folder Source folder of the language, such as `ru`.
 * @returns {string[]} Source paths below the language folder: `ru/staff/orders.md` is listed as `staff/orders.md`.
 */
export const listTranslatedPages = (docsRoot, folder) => {
  return listMarkdown(join(docsRoot, folder))
}

/**
 * Maps every language to the address keys of the pages it really has.
 * @param {string} docsRoot The docs folder.
 * @returns {Record<string, Set<string>>} Language key to address keys; the default language is not listed.
 */
export const translatedKeys = (docsRoot) => {
  const settings = readSettings()
  /** @type {Record<string, Set<string>>} */
  const result = {}
  for (const [key, locale] of Object.entries(settings.locales)) {
    if (key !== settings.default) {
      result[key] = new Set(listTranslatedPages(docsRoot, locale.folder).map(pageKey))
    }
  }
  return result
}

/**
 * Rewrites one link target of an English page so the reader stays in the language.
 * @param {string} target The target as written: a path, a relative path, an address with a host or an anchor.
 * @param {string} source Source path of the page the link is in.
 * @param {string} prefix Folder of the language, such as `ru`.
 * @param {Set<string>} keys Address keys of all pages that exist in every language (the translatable English pages).
 * @param {string[]} prefixes Folders of all other languages.
 * @returns {string} The target below the language folder when that page exists there; an absolute path to the English page when the page is
 * not translatable but the link was relative; otherwise the target as written.
 */
export const rewriteTarget = (target, source, prefix, keys, prefixes) => {
  if (target === '' || target.startsWith('#') || target.startsWith('//') || /^[a-z][a-z0-9+.-]*:/i.test(target)) {
    return target
  }
  const match = /^([^?#]*)([?#].*)?$/.exec(target)
  const path = match ? match[1] : target
  const rest = match?.[2] ?? ''
  const isRelative = !path.startsWith('/')
  const joined = isRelative ? posix.normalize(posix.join(posix.dirname(source), path)) : path
  if (joined.startsWith('..')) {
    return target
  }
  const clean = joined.replace(/^\/+/, '')
  if (prefixes.some((other) => clean === other || clean.startsWith(`${other}/`))) {
    return target
  }
  const withoutExtension = clean.replace(/\.(md|html)$/, '')
  if (/\.[a-z0-9]+$/i.test(withoutExtension.split('/').pop() ?? '')) {
    // A picture, a download or any other file stays where it is.
    return target
  }
  const key = withoutExtension.replace(/(^|\/)index$/, '$1')
  const exists = keys.has(key) || keys.has(`${key}/`) || (key.endsWith('/') && keys.has(key.slice(0, -1)))
  if (exists) {
    const resolved = keys.has(key) ? key : keys.has(`${key}/`) ? `${key}/` : key
    return `/${prefix}/${resolved}${rest}`.replace(/\/\/$/, '/')
  }
  return isRelative ? `/${key}${rest}` : target
}

/**
 * Rewrites the links and includes of the body of an English page.
 * Code fences and inline code are left alone, so an example keeps its text.
 * @param {string} body Markdown body, without the front matter.
 * @param {string} source Source path of the page.
 * @param {string} prefix Folder of the language.
 * @param {Set<string>} keys Address keys of the translatable English pages.
 * @param {string[]} prefixes Folders of all languages other than the default.
 * @returns {string} The body for the placeholder page.
 */
export const rewriteBody = (body, source, prefix, keys, prefixes) => {
  const dir = posix.dirname(source) === '.' ? '' : posix.dirname(source)
  let inFence = false
  return body
    .split('\n')
    .map((line) => {
      if (/^\s*(```|~~~)/.test(line)) {
        inFence = !inFence
        return line
      }
      if (inFence) {
        return line
      }
      // Odd entries are inline code spans.
      return line
        .split(/(`+[^`]*`+)/)
        .map((part, index) => {
          if (index % 2 === 1) {
            return part
          }
          return part
            .replace(/(<!--\s*@include:\s*)(\.{1,2}\/[^\s#{>]+)/g, (_all, head, file) => `${head}@/${englishFolder()}/${posix.join(dir, file)}`)
            .replace(/\]\(\s*(<?)([^)\s>]+)(>?)((?:\s+"[^"]*")?)\s*\)/g, (_all, open, target, close, title) => {
              return `](${open}${rewriteTarget(target, source, prefix, keys, prefixes)}${close}${title})`
            })
            .replace(/(href=")([^"]+)(")/g, (_all, head, target, tail) => `${head}${rewriteTarget(target, source, prefix, keys, prefixes)}${tail}`)
            .replace(/^(\s*\[[^\]]+\]:\s*)(\S+)/, (_all, head, target) => `${head}${rewriteTarget(target, source, prefix, keys, prefixes)}`)
        })
        .join('')
    })
    .join('\n')
}

/**
 * Builds the source of one placeholder page.
 * @param {string} source The English page, whole.
 * @param {string} path Source path of the English page.
 * @param {string} prefix Folder of the language.
 * @param {{ title: string, message: string, original: string }} text The notice in the language.
 * @param {Set<string>} keys Address keys of the translatable English pages.
 * @param {string[]} prefixes Folders of all languages other than the default.
 * @returns {string} Markdown: the front matter of the English page plus `i18nFallback: true`, the notice, then the English body.
 */
export const buildFallbackSource = (source, path, prefix, text, keys, prefixes) => {
  const normalized = source.replace(/\r\n/g, '\n')
  const frontMatter = /^---\n([\s\S]*?)\n---\n?/.exec(normalized)
  const fields = (frontMatter ? frontMatter[1] : '')
    .split('\n')
    .filter((line) => !/^(i18nFallback|lastUpdated):/.test(line))
    .join('\n')
  const body = frontMatter ? normalized.slice(frontMatter[0].length) : normalized
  const original = `/${pageKey(path)}`
  const notice = `::: warning ${text.title}\n${text.message} [${text.original}](${original})\n:::\n\n`
  const head = `---\n${fields ? `${fields}\n` : ''}i18nFallback: true\nlastUpdated: false\n---\n\n`
  return `${head}${notice}${rewriteBody(body, path, prefix, keys, prefixes)}`
}

/**
 * Removes the placeholder folder.
 * @param {string} docsRoot The docs folder.
 */
export const cleanFallbacks = (docsRoot) => {
  const settings = readSettings()
  rmSync(join(docsRoot, settings.fallbackDir), { recursive: true, force: true })
}

/**
 * Writes a placeholder for every English page that a language does not have.
 * @param {string} docsRoot The docs folder.
 * @returns {{ created: Record<string, number>, orphans: string[] }} How many placeholders each language got, and the translated pages
 * that have no English page (they are never reached from the sidebar and are worth a look).
 */
export const generateFallbacks = (docsRoot) => {
  const settings = readSettings()
  cleanFallbacks(docsRoot)
  const pages = listEnglishPages(docsRoot, settings)
  const keys = new Set(pages.map(pageKey))
  const prefixes = Object.values(settings.locales)
    .map((locale) => locale.prefix)
    .filter((prefix) => prefix !== '')
  /** @type {Record<string, number>} */
  const created = {}
  /** @type {string[]} */
  const orphans = []
  for (const [key, locale] of Object.entries(settings.locales)) {
    if (key === settings.default) {
      continue
    }
    const text = readFallbackText(key)
    const own = new Set(listTranslatedPages(docsRoot, locale.folder))
    created[key] = 0
    for (const path of pages) {
      if (own.has(path)) {
        continue
      }
      const target = join(docsRoot, settings.fallbackDir, locale.prefix, ...path.split('/'))
      mkdirSync(dirname(target), { recursive: true })
      const source = readFileSync(join(docsRoot, settings.locales[settings.default].folder, ...path.split('/')), 'utf8')
      writeFileSync(target, buildFallbackSource(source, path, locale.prefix, text, keys, prefixes))
      created[key] += 1
    }
    const known = new Set(pages)
    orphans.push(...[...own].filter((path) => !known.has(path)).map((path) => `${locale.folder}/${path}`))
  }
  return { created, orphans }
}

/**
 * Makes a path relative to the docs folder with `/` separators, for messages.
 * @param {string} docsRoot The docs folder.
 * @param {string} file Absolute path.
 * @returns {string} The relative path.
 */
export const relativeToDocs = (docsRoot, file) => relative(docsRoot, file).split(sep).join('/')
