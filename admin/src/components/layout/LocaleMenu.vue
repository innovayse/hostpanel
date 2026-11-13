<script setup lang="ts">
/**
 * The operator's interface language, as one globe trigger that opens a menu of the shipped languages.
 *
 * The trigger shows a globe and the short code of the active language (`EN`); the menu lists the languages by their
 * own names and marks the active one with a check. It is the shared {@link UiMenu}, so it brings the keyboard
 * behaviour and the radio-item roles. Used by the top bar, the phone menu drawer and the pre-sign-in screens.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Icon } from '@/lib/icons'
import { SUPPORTED_LOCALES, setLocale } from '@/i18n'
import type { SupportedLocale } from '@/i18n'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiMenu from '@/components/ui/UiMenu.vue'
import { iconTriggerClasses } from '@/utils/ui/button'
import type { UiMenuAction } from '@/utils/ui/menu'

/** Props of the language menu. */
withDefaults(defineProps<{
  /** `data-testid` of the trigger; each option is `locale-option-` plus its code. */
  testid?: string
}>(), {
  testid: 'locale-menu',
})

/** Translation function and the active locale. */
const { t, locale } = useI18n()

/** Display labels of the supported languages. */
const localeLabels = computed<Record<SupportedLocale, string>>(() => ({
  en: t('layout.localeLabels.en'),
  hy: t('layout.localeLabels.hy'),
  ru: t('layout.localeLabels.ru'),
}))

/**
 * One menu action per supported language, the active one checked.
 *
 * @returns The languages with their own names.
 */
const actions = computed<UiMenuAction[]>(() =>
  SUPPORTED_LOCALES.map(code => ({
    label: localeLabels.value[code],
    checked: locale.value === code,
    testid: `locale-option-${code}`,
    /**
     * Switches the interface to this language.
     *
     * @returns Nothing; the locale store applies the choice.
     */
    onSelect: () => setLocale(code),
  })),
)
</script>

<template>
  <UiMenu :actions="actions" :aria-label="t('shared.language')" :testid="testid">
    <span :class="[iconTriggerClasses, 'gap-1 px-2']">
      <UiIcon :markup="Icon.Globe" size="sm" />
      <span class="text-xs font-medium uppercase">{{ locale }}</span>
    </span>
  </UiMenu>
</template>
