<template>
  <div ref="wrapperRef" class="relative" data-testid="language-switcher">
    <!-- Trigger: always in the dropdown layout, from xl up in the responsive one -->
    <button
      type="button"
      data-testid="language-switcher-trigger"
      :aria-label="$t('ui.language')"
      :aria-expanded="isOpen"
      :class="triggerClasses"
      @click="isOpen = !isOpen"
    >
      <NuxtImg :src="flagOf(currentLocale.code)" :alt="currentLocale.name" width="20" height="20" class="w-5 h-5 flex-shrink-0" />
      <span class="text-sm font-medium text-gray-900 dark:text-white">{{ currentLocale.code.toUpperCase() }}</span>
      <ChevronDown :size="14" :stroke-width="2" class="text-gray-500 dark:text-gray-400 transition-transform duration-200" :class="isOpen ? 'rotate-180' : ''" />
    </button>

    <!-- Cards: the responsive layout below xl -->
    <div v-if="isResponsive" class="xl:hidden grid grid-cols-3 gap-1.5 sm:gap-2">
      <button
        v-for="loc in availableLocales"
        :key="loc.code"
        type="button"
        :data-testid="`language-card-${loc.code}`"
        :class="[
          'flex flex-row items-center justify-center gap-2 px-3 sm:px-4 py-2.5 sm:py-3 rounded-lg transition-all duration-100',
          FOCUS_RING,
          locale === loc.code
            ? 'bg-primary-500/20 ring-2 ring-primary-500 shadow-lg shadow-primary-500/30'
            : 'bg-gray-100 dark:bg-white/5 hover:bg-gray-200 dark:hover:bg-white/10 border border-gray-200 dark:border-white/10'
        ]"
        @click="switchLanguage(loc.code)"
      >
        <NuxtImg :src="flagOf(loc.code)" :alt="loc.name" width="32" height="32" class="w-6 h-6 sm:w-7 sm:h-7 flex-shrink-0" />
        <span class="text-sm sm:text-base font-bold text-gray-900 dark:text-white whitespace-nowrap">{{ loc.code.toUpperCase() }}</span>
      </button>
    </div>

    <!-- Menu -->
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="opacity-0 scale-95"
      enter-to-class="opacity-100 scale-100"
      leave-active-class="transition duration-100 ease-in"
      leave-from-class="opacity-100 scale-100"
      leave-to-class="opacity-0 scale-95"
    >
      <div
        v-if="isOpen"
        role="menu"
        :class="menuClasses"
      >
        <button
          v-for="loc in availableLocales"
          :key="loc.code"
          type="button"
          role="menuitem"
          :data-testid="`language-option-${loc.code}`"
          class="w-full flex items-center gap-3 px-4 py-2.5 text-sm transition-colors duration-150 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500"
          :class="locale === loc.code
            ? 'bg-primary-100 dark:bg-primary-500/25 text-primary-700 dark:text-primary-300 font-medium'
            : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 hover:text-gray-900 dark:hover:text-white'"
          @click="switchLanguage(loc.code)"
        >
          <NuxtImg :src="flagOf(loc.code)" :alt="loc.name" width="20" height="20" class="w-5 h-5 flex-shrink-0" />
          <span class="flex-1 text-left">{{ loc.name }}</span>
          <Check v-if="locale === loc.code" :size="14" :stroke-width="2.5" class="text-primary-500 dark:text-primary-400 flex-shrink-0" />
        </button>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
/**
 * LanguageSwitcher (`UiLanguageSwitcher`) - the one language picker of the portal.
 *
 * Two layouts share one implementation: `dropdown` is a trigger with a menu, and `responsive` is
 * the same dropdown from `xl` up with a row of flag cards below it (used in the mobile menu).
 * Switching reloads the page on the localised path so SSR and third-party widgets such as the
 * live-chat SDK re-initialise in the new language.
 */
import { computed, ref } from 'vue'
import { ChevronDown, Check } from 'lucide-vue-next'
import { onClickOutside } from '@vueuse/core'
import type { AppLocale } from '~/types/applocale'
import { UiLanguageLayout, UiMenuAlign, UiPlacement } from '~/utils/ui/enums'
import { menuAlignClasses } from '~/utils/ui/menuAlign'
import { FOCUS_RING, PICKER_TRIGGER } from '~/utils/ui/tones'

/** Props of LanguageSwitcher. */
interface Props {
  /** Whether the menu opens below or above the trigger. */
  placement?: `${UiPlacement}`
  /** `dropdown` always, or `responsive` (dropdown from xl, cards below). */
  layout?: `${UiLanguageLayout}`
  /** Edge of the trigger the menu lines up with: `end` (default) for the right end of a header. */
  align?: `${UiMenuAlign}`
  /** Replaces the default tinted frame of the trigger (shape, spacing and colours); focus and display stay. */
  triggerClass?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  placement: UiPlacement.Bottom,
  layout: UiLanguageLayout.Dropdown,
  align: UiMenuAlign.End,
  triggerClass: undefined,
})

/** Flag image per locale code; unknown codes get the neutral flag. */
const FLAGS: Record<string, string> = {
  en: '/flags/us.svg',
  ru: '/flags/ru.svg',
  hy: '/flags/am.svg',
}

/** Active locale code and the locales the portal offers. */
const { locale, locales } = useI18n()
/** Builds the localised path for a locale code. */
const switchLocalePath = useSwitchLocalePath()
/** Router used for in-app navigation on the server path. */
const router = useRouter()

/** Whether the menu is showing. */
const isOpen = ref(false)

/** The root element, used to detect outside clicks. */
const wrapperRef = ref<HTMLElement | null>(null)

onClickOutside(wrapperRef, () => {
  isOpen.value = false
})

/** Whether the card row is part of this switcher. */
const isResponsive = computed(() => props.layout === UiLanguageLayout.Responsive)

/** Every locale the portal offers. */
const availableLocales = computed(() => locales.value)

/** The locale entry matching the active code (first entry, then the bare code, as fallbacks). */
const currentLocale = computed(() =>
  locales.value.find((entry) => entry.code === locale.value) ?? locales.value.at(0) ?? { code: locale.value, name: locale.value },
)

/** Class list of the trigger. */
const triggerClasses = computed(() => [
  props.triggerClass ?? `items-center gap-2 px-3 py-2 rounded-lg ${PICKER_TRIGGER}`,
  FOCUS_RING,
  isResponsive.value ? 'hidden xl:flex' : 'flex',
].join(' '))

/** Class list of the menu panel. */
const menuClasses = computed(() => [
  menuAlignClasses(props.align),
  'w-44 rounded-xl border border-gray-200 dark:border-white/15 bg-ui-popover shadow-xl shadow-black/10 dark:shadow-black/40 overflow-hidden py-1 z-50',
  isResponsive.value ? 'hidden xl:block' : '',
  props.placement === UiPlacement.Top ? 'bottom-full mb-2' : 'top-full mt-2',
].join(' '))

/**
 * Flag image for a locale code.
 *
 * @param code Locale code.
 * @returns Path of the flag SVG.
 */
const flagOf = (code: string): string => FLAGS[code] ?? '/flags/un.svg'

/**
 * Switches the portal to another language.
 *
 * @param code Locale code to switch to.
 */
const switchLanguage = async (code: string): Promise<void> => {
  const path = switchLocalePath(code as AppLocale)

  // A hard reload keeps SSR and third-party widgets (the live-chat SDK) consistent with the new language.
  if (import.meta.client) {
    window.location.href = path
  } else {
    await router.push(path)
  }

  isOpen.value = false
}
</script>
