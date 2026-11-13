<script setup lang="ts">
/**
 * The operator's light, dark or system theme choice, as one icon trigger that opens a menu of the three themes.
 *
 * The trigger shows the icon of the current mode (sun, moon or screen); the menu marks the current theme with a check
 * and is the shared {@link UiMenu}, so it brings the keyboard behaviour and the radio-item roles. Used by the top bar,
 * the phone menu drawer and the pre-sign-in screens.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Icon } from '@/lib/icons'
import { useThemeStore } from '@/stores/theme'
import { AdminTheme } from '@/types/admintheme'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiMenu from '@/components/ui/UiMenu.vue'
import { iconTriggerClasses } from '@/utils/ui/button'
import type { UiMenuAction } from '@/utils/ui/menu'

/** Props of the theme menu. */
withDefaults(defineProps<{
  /** `data-testid` of the trigger; each option is `theme-option-` plus its theme. */
  testid?: string
}>(), {
  testid: 'theme-menu',
})

/** Translation function. */
const { t } = useI18n()

/** Theme store: the operator's light/dark/system choice. */
const theme = useThemeStore()

/** Icon of each theme, shown on the trigger for the current one and beside its option. */
const THEME_ICONS: Record<AdminTheme, string> = {
  [AdminTheme.Light]: Icon.Sun,
  [AdminTheme.Dark]: Icon.Moon,
  [AdminTheme.System]: Icon.Monitor,
}

/**
 * One menu action per {@link AdminTheme}, the current one checked.
 *
 * @returns The three choices with their labels in the active language.
 */
const actions = computed<UiMenuAction[]>(() => [
  { value: AdminTheme.Light, label: t('layout.theme.light') },
  { value: AdminTheme.Dark, label: t('layout.theme.dark') },
  { value: AdminTheme.System, label: t('layout.theme.system') },
].map(option => ({
  label: option.label,
  icon: THEME_ICONS[option.value],
  checked: theme.choice === option.value,
  testid: `theme-option-${option.value}`,
  /**
   * Applies this theme choice.
   *
   * @returns Nothing; the theme store applies the choice.
   */
  onSelect: () => theme.setTheme(option.value),
})))
</script>

<template>
  <UiMenu :actions="actions" :aria-label="t('layout.theme.label')" :testid="testid">
    <span :class="iconTriggerClasses">
      <UiIcon :markup="THEME_ICONS[theme.choice]" size="sm" />
    </span>
  </UiMenu>
</template>
