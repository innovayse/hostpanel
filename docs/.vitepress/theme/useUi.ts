/**
 * The interface texts of the open page's language, for the custom components of the theme. The language
 * comes from the `lang` the site config sets for each locale, so a component has no English of its own.
 */
import { useData } from 'vitepress'
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { localeOfLang, stringsForLang } from '../i18n/ui'
import type { UiStrings } from '../i18n/ui'
import type { LocaleKey } from '../i18n/locales'

/** What `useUi` returns. */
export interface UseUi {
  /** The texts of the open page's language; reactive, so they follow a change of language. */
  ui: ComputedRef<UiStrings>
  /** The open page's language. */
  locale: ComputedRef<LocaleKey>
}

/**
 * Reads the language of the open page.
 * @returns The texts and the language, both reactive; English when the page has no language.
 */
export const useUi = (): UseUi => {
  const data = useData() as { lang?: { value: string } }
  /** The open page's language key. */
  const locale = computed<LocaleKey>(() => localeOfLang(data.lang?.value))
  /** The interface texts of that language. */
  const ui = computed<UiStrings>(() => stringsForLang(data.lang?.value))
  return { ui, locale }
}
