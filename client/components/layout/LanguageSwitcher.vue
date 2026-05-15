<template>
  <div class="relative" v-click-outside="closeDropdown">
    <!-- Language button - Desktop only (1280px+) -->
    <button
      type="button"
      class="hidden xl:flex items-center gap-2 px-3 py-2 rounded-lg bg-white/5 border border-white/10 hover:border-primary-500/30 hover:bg-white/10 transition-all duration-200"
      @click="toggleDropdown"
    >
      <NuxtImg
        :src="getFlagImage(currentLocale.code)"
        :alt="currentLocale.name"
        width="20"
        height="20"
        format="webp"
        quality="80"
        loading="eager"
        class="w-5 h-5"
      />
      <span class="text-sm font-medium text-white">{{ currentLocale.code.toUpperCase() }}</span>
      <ChevronDown
        :size="16"
        :stroke-width="2"
        class="text-gray-400 transition-transform duration-200"
        :class="{ 'rotate-180': isOpen }"
      />
    </button>

    <!-- Mobile Cards - Always visible (below 1280px) -->
    <div class="xl:hidden grid grid-cols-3 gap-1.5 sm:gap-2">
      <button
        v-for="locale in availableLocales"
        :key="locale.code"
        type="button"
        :class="[
          'flex flex-row items-center justify-center gap-2 px-3 sm:px-4 py-2.5 sm:py-3 rounded-lg transition-all duration-100',
          currentLocale.code === locale.code
            ? 'bg-primary-500/20 ring-2 ring-primary-500 shadow-lg shadow-primary-500/30'
            : 'bg-white/5 hover:bg-white/10 border border-white/10'
        ]"
        @click="switchLanguage(locale.code)"
      >
        <NuxtImg
          :src="getFlagImage(locale.code)"
          :alt="locale.name"
          width="32"
          height="32"
          format="webp"
          quality="80"
          loading="eager"
          class="w-6 h-6 sm:w-7 sm:h-7 flex-shrink-0"
        />
        <span class="text-sm sm:text-base font-bold text-white whitespace-nowrap">{{ locale.code.toUpperCase() }}</span>
      </button>
    </div>

    <!-- Desktop Dropdown (1280px+) -->
    <Transition name="dropdown">
      <div
        v-show="isOpen"
        class="hidden xl:block absolute right-0 mt-2 w-48 rounded-xl bg-[#1a1a1f] border-2 border-gray-700 shadow-2xl shadow-black/50 overflow-hidden z-50"
      >
        <button
          v-for="locale in availableLocales"
          :key="locale.code"
          type="button"
          :class="[
            'w-full px-4 py-3 text-left text-sm transition-all duration-200 flex items-center gap-3',
            currentLocale.code === locale.code
              ? 'bg-primary-500 text-white font-semibold'
              : 'text-gray-300 hover:bg-white/10 hover:text-white'
          ]"
          @click="switchLanguage(locale.code)"
        >
          <NuxtImg
            :src="getFlagImage(locale.code)"
            :alt="locale.name"
            width="24"
            height="24"
            format="webp"
            quality="80"
            loading="eager"
            class="w-6 h-6"
          />
          <span class="flex-1">{{ locale.name }}</span>
          <Check
            v-if="currentLocale.code === locale.code"
            :size="18"
            :stroke-width="2"
          />
        </button>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
/**
 * Language switcher dropdown
 */

import { ChevronDown, Check } from 'lucide-vue-next'

const { locale, locales } = useI18n()
const switchLocalePath = useSwitchLocalePath()
const router = useRouter()
const isOpen = ref(false)

const availableLocales = computed(() => locales.value)
const currentLocale = computed(() => {
  return locales.value.find(l => l.code === locale.value) || locales.value[0]!
})

const toggleDropdown = () => {
  isOpen.value = !isOpen.value
}

const closeDropdown = () => {
  isOpen.value = false
}

const switchLanguage = async (code: 'en' | 'ru' | 'hy') => {
  const path = switchLocalePath(code)
  
  // Use hard reload for language switching to ensure proper SSR 
  // and re-initialization of third-party widgets like InnoChat
  if (process.client) {
    window.location.href = path
  } else {
    await router.push(path)
  }
  
  closeDropdown()
}

// Flag emojis for each language
const getFlag = (code: string) => {
  const flags: Record<string, string> = {
    en: '🇺🇸',
    ru: '🇷🇺',
    hy: '🇦🇲'
  }
  return flags[code] || '🌐'
}

// Flag images for mobile cards
const getFlagImage = (code: string) => {
  const flagImages: Record<string, string> = {
    en: '/flags/us.svg',
    ru: '/flags/ru.svg',
    hy: '/flags/am.svg'
  }
  return flagImages[code] || '/flags/un.svg'
}

const currentFlag = computed(() => getFlag(currentLocale.value.code))

// Click outside directive
interface ClickOutsideElement extends HTMLElement {
  clickOutsideEvent?: (event: Event) => void
}

const vClickOutside = {
  mounted(el: ClickOutsideElement, binding: any) {
    el.clickOutsideEvent = (event: Event) => {
      if (!(el === event.target || el.contains(event.target as Node))) {
        binding.value()
      }
    }
    document.addEventListener('click', el.clickOutsideEvent)
  },
  unmounted(el: ClickOutsideElement) {
    if (el.clickOutsideEvent) {
      document.removeEventListener('click', el.clickOutsideEvent)
    }
  }
}
</script>

<style scoped>
.dropdown-enter-active,
.dropdown-leave-active {
  transition: all 0.2s ease;
}

.dropdown-enter-from {
  opacity: 0;
  transform: translateY(-10px);
}

.dropdown-leave-to {
  opacity: 0;
  transform: translateY(-5px);
}
</style>
