/**
 * The en / ru / hy tab strip of a storefront content form: the tab items, the selected locale and
 * the handler that narrows the strip's string back to a locale.
 */

import { computed, ref } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { enumValueOf } from '@/lib/enumValue'
import { MessageLanguage } from '@/locales/messages'
import type { UiTabItem } from '@/utils/ui/tabs'

/** What {@link useStorefrontLocaleTabs} hands the form. */
export interface StorefrontLocaleTabs {
  /** The locale whose fields are on screen. */
  activeLocale: Ref<MessageLanguage>
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
  const activeLocale = ref<MessageLanguage>(MessageLanguage.En)

  /** One tab per locale. */
  const tabs = computed<UiTabItem[]>(() => [
    { value: MessageLanguage.En, label: t('storefront.locales.en'), testid: `${testidPrefix}-en` },
    { value: MessageLanguage.Ru, label: t('storefront.locales.ru'), testid: `${testidPrefix}-ru` },
    { value: MessageLanguage.Hy, label: t('storefront.locales.hy'), testid: `${testidPrefix}-hy` },
    { value: MessageLanguage.Es, label: t('storefront.locales.es'), testid: `${testidPrefix}-es` },
  ])

  /**
   * Switches the translation tab.
   *
   * @param value - The tab value the strip reported.
   */
  const select = (value: string): void => {
    const locale = enumValueOf(MessageLanguage, value)
    if (locale !== null) {
      activeLocale.value = locale
    }
  }

  return { activeLocale, tabs, select }
}
