<script setup lang="ts">
/**
 * The operator's light or dark theme, as one icon button that switches between the two on a click.
 *
 * The icon shows the theme the panel is in now (moon in dark, sun in light); the button is pressed while the dark theme
 * is on, and its one accessible name stays the same, so a screen reader announces the state instead of a changing label.
 * Used by the top bar, the phone menu drawer and the pre-sign-in screens.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Icon } from '@/lib/icons'
import { useThemeStore } from '@/stores/theme'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import { iconTriggerIconSize } from '@/utils/ui/button'

/** Props of the theme toggle. */
withDefaults(defineProps<{
  /** `data-testid` of the button. */
  testid?: string
}>(), {
  testid: 'theme-toggle',
})

/** Translation function. */
const { t } = useI18n()

/** Theme store: the applied scheme and the switch. */
const theme = useThemeStore()

/**
 * Icon of the theme in use.
 *
 * @returns The moon while the dark theme is on, the sun while the light one is.
 */
const currentIcon = computed<string>(() => (theme.isLight ? Icon.Sun : Icon.Moon))
</script>

<template>
  <UiIconButton
    :label="t('layout.theme.label')"
    :aria-pressed="!theme.isLight"
    :data-testid="testid"
    @click="theme.toggle()"
  >
    <UiIcon :markup="currentIcon" :size="iconTriggerIconSize" />
  </UiIconButton>
</template>
