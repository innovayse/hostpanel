/**
 * The tab strip (en / ru / hy / de / es) of a storefront content form: the tab items, the selected locale and
 * the handler that narrows the strip's string back to a locale.
 */

import { computed, ref } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { enumValueOf } from '@/lib/enumValue'
import { ContentLanguage } from '@/locales/contentLanguage'
import type { UiTabItem } from '@/utils/ui/tabs'

/** What {@link useStorefrontLocaleTabs} hands the form. */
export interface StorefrontLocaleTabs {
  /** The locale whose fields are on screen. */
  activeLocale: Ref<ContentLanguage>
  /** One tab per locale. */
  tabs: ComputedRef<UiTabItem[]>
  /** Switches the locale from the value the strip reported. */
  select: (value: string) => void
}

/**
 * Creates the locale tab state of a form.
 *
 * @param testidPrefix - Prefix of the tab test ids, e.g. `testimonial-form-tab` gives `testimonial-form-tab-en`.
 * @returns The selected locale, the tab items and the selection handler.
 */
export const useStorefrontLocaleTabs = (testidPrefix: string): StorefrontLocaleTabs => {
  const { t } = useI18n()

  /** The locale whose fields are on screen. */
  const activeLocale = ref<ContentLanguage>(ContentLanguage.En)

  /** One tab per locale. */
  const tabs = computed<UiTabItem[]>(() => [
    { value: ContentLanguage.En, label: t('storefront.locales.en'), testid: `${testidPrefix}-en` },
    { value: ContentLanguage.Ru, label: t('storefront.locales.ru'), testid: `${testidPrefix}-ru` },
    { value: ContentLanguage.Hy, label: t('storefront.locales.hy'), testid: `${testidPrefix}-hy` },
    { value: ContentLanguage.De, label: t('storefront.locales.de'), testid: `${testidPrefix}-de` },
    { value: ContentLanguage.Es, label: t('storefront.locales.es'), testid: `${testidPrefix}-es` },
  ])

  /**
   * Switches the translation tab.
   *
   * @param value - The tab value the strip reported.
   */
  const select = (value: string): void => {
    const locale = enumValueOf(ContentLanguage, value)
    if (locale !== null) {
      activeLocale.value = locale
    }
  }

  return { activeLocale, tabs, select }
}
