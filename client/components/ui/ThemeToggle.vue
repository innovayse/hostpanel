<template>
  <!--
    Rendered only while the operator allows visitors to switch. The composable's
    toggle() is a no-op in that case too, so this is the visible half of one rule.

    Both icons are in the markup and the dark: variant shows one: under the `system`
    default the server renders as dark, and production Vue does not patch an icon
    that disagrees after hydration — so a v-if on isDark left a light-device visitor
    looking at the sun. The class swap follows <html class> and cannot disagree with it.
    The title is set after mount for the same reason.
  -->
  <button
    v-if="userToggleEnabled"
    type="button"
    :title="title"
    :aria-label="title"
    class="flex items-center justify-center w-9 h-9 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:border-gray-300 dark:hover:border-white/20 transition-all duration-200 bg-gray-100 dark:bg-white/5"
    @click="toggle"
  >
    <Sun :size="16" :stroke-width="2" class="hidden dark:block" />
    <Moon :size="16" :stroke-width="2" class="block dark:hidden" />
  </button>
</template>

<script setup lang="ts">
/**
 * Sun/moon switch between light and dark mode.
 *
 * Hidden entirely when `portal.theme.user_toggle` is `false` — an operator who
 * has designed a light-only or dark-only storefront does not want a visitor
 * flipping it into the palette they never checked.
 */
import { Sun, Moon } from 'lucide-vue-next'

const { t } = useI18n()
const { isDark, userToggleEnabled, toggle } = useAppColorMode()

/** False until the browser knows the mode; the server may not (see the template note). */
const mounted = ref(false)
onMounted(() => { mounted.value = true })

/** Tooltip and accessible name, only once the mode is certain. */
const title = computed(() =>
  mounted.value ? (isDark.value ? t('ui.theme.switchLight') : t('ui.theme.switchDark')) : undefined)
</script>
