/**
 * The sections a legal page shows: the operator's own text when it has stated one, otherwise the
 * built-in text.
 *
 * The operator writes the text in the admin (Settings), one row per language. The page reads its own
 * rows once, from the legal text read (never from the settings every page carries); a language
 * without its own row shows the default row, and a page with no text at all keeps the built-in copy, so
 * an install that sets nothing renders exactly what it always did. The merge lives here once, so the
 * five pages cannot decide it five ways.
 *
 * @module composables/useLegalSections
 */

import type { ComputedRef } from 'vue'
import { LEGAL_PAGE_SOURCES } from '~/constants/legalPage'
import type { LegalPage } from '~/constants/legalPage'
import { useContentStore } from '~/stores/content'
import type { LegalSection } from '~/types/legalsection'
import { parseLegalText } from '~/utils/legalText'
import { localizedSettingKey } from '~/utils/localizedSetting'

/** What {@link useLegalSections} hands the page. */
export interface LegalSections {
  /** The sections to draw: the operator's, or the built-in ones when it set none. */
  sections: ComputedRef<LegalSection[]>
  /** Whether the sections are the operator's own text, which the built-in "last updated" date does not describe. */
  isCustom: ComputedRef<boolean>
}

/**
 * Resolves the sections of one legal page, reading the operator's text of that page first.
 *
 * @param page The page being drawn.
 * @param builtIn The page's built-in sections, in the active language.
 * @returns The sections to draw and whether they are the operator's, once the text has been read.
 */
export const useLegalSections = async (page: LegalPage, builtIn: ComputedRef<LegalSection[]>): Promise<LegalSections> => {
  const content = useContentStore()
  const { t, locale } = useI18n()
  const source = LEGAL_PAGE_SOURCES[page]

  // Every language arrives together, so a language change reads nothing again.
  await useAsyncData(`legal:${page}`, () => content.loadLegalText(page))

  /** The operator's text in the active language, as sections; empty when it set none. */
  const custom = computed<LegalSection[]>(() => {
    const texts = content.legalTextFor(page)
    const text = texts[localizedSettingKey(source.key, locale.value)] || texts[source.key] || ''
    return parseLegalText(text, t(source.titleKey))
  })

  /** Whether the operator's own text replaces the built-in one. */
  const isCustom = computed(() => custom.value.length > 0)

  /** The sections to draw. */
  const sections = computed<LegalSection[]>(() => (isCustom.value ? custom.value : builtIn.value))

  return { sections, isCustom }
}
