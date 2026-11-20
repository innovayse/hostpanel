/**
 * The admin message catalogue, assembled from `locales/<lang>/<area>.json`.
 *
 * Each file is one area; its file name is the first segment of every key in it, so `nav.json`
 * holds the keys `nav.*` and a key is addressed as `t('nav.dashboard')`. Adding an area is adding
 * a file to all three language folders; `npm run lint:i18n` fails when one language lacks it.
 */

import { ContentLanguage } from '@/locales/contentLanguage'

/** A nested message tree as stored in one area file. */
export type MessageTree = { [key: string]: string | MessageTree }

/**
 * Languages the catalogue ships; one folder each. A subset of {@link ContentLanguage}, the one list of
 * languages: a language the interface is translated into is a content language too, never the other way round.
 */
export const MessageLanguage = {
  /** English. */
  En: ContentLanguage.En,
  /** Russian. */
  Ru: ContentLanguage.Ru,
  /** Armenian. */
  Hy: ContentLanguage.Hy,
} as const

/** A language the admin interface is translated into. */
export type MessageLanguage = (typeof MessageLanguage)[keyof typeof MessageLanguage]

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
