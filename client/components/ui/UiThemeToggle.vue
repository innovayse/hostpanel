<template>
  <!--
    Rendered only while the operator allows visitors to switch. The composable's
    toggle() is a no-op in that case too, so this is the visible half of one rule.

    The icon shows the mode the page is in NOW (moon in dark, sun in light); the accessible
    name and the tooltip say what a click does. Both icons are in the markup and the dark: variant shows one: under the `system`
    default the server renders as dark, and production Vue does not patch an icon
    that disagrees after hydration, so a v-if on isDark left a light-device visitor
    looking at the sun. The class swap follows <html class> and cannot disagree with it.
    The title is set after mount for the same reason.
  -->
  <UiIconButton
    v-if="userToggleEnabled"
    testid="theme-toggle"
    :label="title ?? $t('ui.theme.switchDark')"
    :title="title"
    :variant="variant"
    :size="size"
    @click="toggle"
  >
    <Moon :size="iconSize" :stroke-width="2" class="hidden dark:block" data-testid="theme-toggle-moon" />
    <Sun :size="iconSize" :stroke-width="2" class="block dark:hidden" data-testid="theme-toggle-sun" />
  </UiIconButton>
</template>

<script setup lang="ts">
/**
 * ThemeToggle (`UiThemeToggle`): sun/moon switch between light and dark mode.
 *
 * Hidden entirely when `portal.theme.user_toggle` is `false`: an operator who has designed a
 * light-only or dark-only storefront does not want a visitor flipping it into the palette they
 * never checked.
 */
import { computed, onMounted, ref } from 'vue'
import { Sun, Moon } from 'lucide-vue-next'
import { UiIconButtonVariant, UiSize } from '~/utils/ui/enums'

/** Props of ThemeToggle. */
interface Props {
  /** Look of the underlying icon button; `bare` lets the screen paint the frame through its class. */
  variant?: `${UiIconButtonVariant}`
  /** Size step of the underlying icon button. */
  size?: `${UiSize}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  variant: UiIconButtonVariant.Subtle,
  size: UiSize.Md,
})

/** Pixel size of the glyph: the large button (the classic header's control height) takes the larger icon. */
const iconSize = computed(() => (props.size === UiSize.Lg ? 20 : 16))

/** Translator for the strings this component builds itself. */
const { t } = useI18n()
/** Current color mode, whether visitors may switch it, and the switch action. */
const { isDark, applied, userToggleEnabled, toggle } = useAppColorMode()

/** False until the browser knows the mode; the server may not (see the template note). */
const mounted = ref(false)
onMounted(() => {
  mounted.value = true
})

/**
 * Tooltip and accessible name, only once the mode is certain: after mount, or from the first
 * render when the server already knew it (a saved choice or a fixed operator default), so a
 * dark page is never announced as "switch to dark mode".
 */
const title = computed(() =>
  mounted.value || applied.value !== null ? (isDark.value ? t('ui.theme.switchLight') : t('ui.theme.switchDark')) : undefined)
</script>
