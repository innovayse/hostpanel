/**
 * The header of a screen read from its skeleton schema.
 *
 * A schema names the title, the subtitle and the back link caption of its screen as locale keys. The real screen resolves
 * them here, so the header it draws and the one the skeleton draws while the chunk downloads are the same text; a screen
 * overrides only what is made of data (a client name, an edit or create wording chosen at run time).
 */
import type { SkeletonHeader } from '@/utils/ui/skeletonSchema'

/** Resolves a locale key to its text; the `t` of `useI18n`. */
type HeaderTranslate = (key: string) => string

/** The props of `UiPageHeader` a schema header decides; bind it with `v-bind`. */
export interface SchemaHeader {
  /** Translated title; absent when the schema names none. */
  title?: string
  /** Translated subtitle; absent when the schema names none. */
  subtitle?: string
  /** Translated name of the place the back link returns to; absent when the schema names none. */
  backLabel?: string
  /** Whether the back link shows only below `md`. */
  backMobileOnly?: boolean
}

/** What a screen overrides at run time: a title or a subtitle that is made of data or chosen by a mode. */
export type SchemaHeaderOverrides = Pick<SchemaHeader, 'title' | 'subtitle'>

/**
 * Translates the header of a screen schema.
 *
 * @param schema - The screen's schema; one without a header gives an empty result.
 * @param schema.header - The header of the schema, when it has one.
 * @param t - Resolves a locale key.
 * @param overrides - The title and the subtitle the screen decides itself; an absent one keeps the schema's.
 * @returns The props for `UiPageHeader`.
 */
export const schemaHeader = (schema: { header?: SkeletonHeader }, t: HeaderTranslate, overrides: SchemaHeaderOverrides = {}): SchemaHeader => {
  const header = schema.header
  return {
    title: overrides.title ?? (header?.title === undefined ? undefined : t(header.title)),
    subtitle: overrides.subtitle ?? (header?.subtitle === undefined ? undefined : t(header.subtitle)),
    backLabel: header?.backLabel === undefined ? undefined : t(header.backLabel),
    backMobileOnly: header?.backMobileOnly,
  }
}
