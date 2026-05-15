<template>
  <div ref="wrapperRef" class="relative">
    <button
      type="button"
      class="flex items-center gap-2 px-3 py-2 rounded-lg bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 hover:border-primary-500/30 hover:bg-gray-200 dark:hover:bg-white/10 transition-all duration-200"
      @click="isOpen = !isOpen"
    >
      <NuxtImg :src="flagOf(currentLocale.code)" :alt="currentLocale.name" width="18" height="18" class="w-[18px] h-[18px] flex-shrink-0" />
      <span class="text-sm font-medium text-gray-900 dark:text-white">{{ currentLocale.code.toUpperCase() }}</span>
      <ChevronDown :size="14" :stroke-width="2" class="text-gray-500 dark:text-gray-400 transition-transform duration-200" :class="isOpen ? 'rotate-180' : ''" />
    </button>

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
        class="absolute left-0 w-44 rounded-xl border border-gray-200 dark:border-white/15 bg-white dark:bg-[#0d0d14] shadow-xl shadow-black/10 dark:shadow-black/40 overflow-hidden py-1 z-50"
        :class="placement === 'top' ? 'bottom-full mb-2' : 'top-full mt-2'"
      >
        <button
          v-for="loc in availableLocales"
          :key="loc.code"
          type="button"
          class="w-full flex items-center gap-3 px-4 py-2.5 text-sm transition-colors duration-150"
          :class="locale === loc.code
            ? 'bg-primary-50 dark:bg-primary-500/20 text-primary-600 dark:text-primary-300'
            : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/8 hover:text-gray-900 dark:hover:text-white'"
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
import { ChevronDown, Check } from 'lucide-vue-next'
import { onClickOutside } from '@vueuse/core'

interface Props {
  placement?: 'bottom' | 'top'
}

withDefaults(defineProps<Props>(), { placement: 'bottom' })

const { locale, locales } = useI18n()
const switchLocalePath = useSwitchLocalePath()
const router = useRouter()

const isOpen = ref(false)
const wrapperRef = ref<HTMLElement | null>(null)
onClickOutside(wrapperRef, () => { isOpen.value = false })

const availableLocales = computed(() => locales.value)
const currentLocale = computed(() =>
  locales.value.find(l => l.code === locale.value) ?? locales.value[0]!
)

function flagOf(code: string): string {
  return ({ en: '/flags/us.svg', ru: '/flags/ru.svg', hy: '/flags/am.svg' } as Record<string, string>)[code] ?? '/flags/un.svg'
}

async function switchLanguage(code: string) {
  const path = switchLocalePath(code as 'en' | 'ru' | 'hy')
  
  // Use hard reload for language switching to ensure proper SSR 
  // and re-initialization of third-party widgets like InnoChat
  if (process.client) {
    window.location.href = path
  } else {
    await router.push(path)
  }
  
  isOpen.value = false
}
</script>
