/**
 * The admin message catalogue, assembled from `locales/<lang>/<area>.json`.
 *
 * Each file is one area; its file name is the first segment of every key in it, so `nav.json`
 * holds the keys `nav.*` and a key is addressed as `t('nav.dashboard')`. Adding an area is adding
 * a file to all three language folders; `npm run lint:i18n` fails when one language lacks it.
 */

/** A nested message tree as stored in one area file. */
export type MessageTree = { [key: string]: string | MessageTree }

/** Languages the catalogue ships; one folder each. Also the languages storefront content is translated into. */
export enum MessageLanguage {
  /** English. */
  En = 'en',
  /** Russian. */
  Ru = 'ru',
  /** Armenian. */
  Hy = 'hy',
  /** Spanish. */
  Es = 'es',
}

/** Message trees of one language, keyed by area name. */
export type LanguageMessages = Record<string, MessageTree>

/** Every area file, keyed by its path relative to this folder (`./en/nav.json`). */
const modules = import.meta.glob<MessageTree>('./*/*.json', { eager: true, import: 'default' })

/**
 * Groups area files into per-language message trees.
 *
 * @param files - Area files keyed by `./<lang>/<area>.json`.
 * @returns Messages per language, each keyed by area name.
 */
export const buildMessages = (files: Record<string, MessageTree>): Record<MessageLanguage, LanguageMessages> => {
  const result: Record<MessageLanguage, LanguageMessages> = {
    [MessageLanguage.En]: {},
    [MessageLanguage.Ru]: {},
    [MessageLanguage.Hy]: {},
    [MessageLanguage.Es]: {},
  }
  for (const [path, tree] of Object.entries(files)) {
    const match = /^\.\/([^/]+)\/([^/]+)\.json$/.exec(path)
    const language = match?.[1]
    const area = match?.[2]
    if (language && area && language in result) {
      const code = language as MessageLanguage
      result[code] = { ...result[code], [area]: tree }
    }
  }
  return result
}

/** All shipped messages: `messages.en.nav` is the English `nav` area. */
export const messages: Record<MessageLanguage, LanguageMessages> = buildMessages(modules)
