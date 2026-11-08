<script setup lang="ts">
/**
 * The operator's light, dark or system theme choice, as one select.
 *
 * The top bar shows it from the small breakpoint up; on a phone the menu drawer has `ThemeSwitch` instead, so a
 * phone is never left without a way to change the theme. The caller's `data-testid` and width classes fall
 * through to the select.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useThemeStore } from '@/stores/theme'
import { AdminTheme } from '@/types/admintheme'
import UiSelect from '@/components/ui/UiSelect.vue'

/** Props of the theme select. */
withDefaults(defineProps<{
  /** The small toolbar look of the select; forms keep the default size. */
  compact?: boolean
}>(), {
  compact: false,
})

/** Translation function. */
const { t } = useI18n()

/** Theme store: the operator's light/dark/system choice. */
const theme = useThemeStore()

/**
 * Options of the select, one per {@link AdminTheme}.
 *
 * @returns The three choices with their labels in the active language.
 */
const themeOptions = computed(() => [
  { value: AdminTheme.Light, label: t('layout.theme.light') },
  { value: AdminTheme.Dark, label: t('layout.theme.dark') },
  { value: AdminTheme.System, label: t('layout.theme.system') },
])

/**
 * Applies the theme the operator picked, when it is one the panel knows.
 *
 * @param value - The value picked in the select.
 * @returns Nothing; the theme store is updated.
 */
const chooseTheme = (value: string): void => {
  const chosen = Object.values(AdminTheme).find(known => known === value)
  if (chosen !== undefined) {
    theme.setTheme(chosen)
  }
}
</script>

<template>
  <UiSelect
    :model-value="theme.choice"
    :options="themeOptions"
    :placeholder="t('layout.theme.label')"
    :aria-label="t('layout.theme.label')"
    :compact="compact"
    @update:model-value="chooseTheme"
  />
</template>
