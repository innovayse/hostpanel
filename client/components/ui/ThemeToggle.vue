<template>
  <button
    type="button"
    :title="isDark ? $t('ui.theme.switchLight') : $t('ui.theme.switchDark')"
    class="flex items-center justify-center w-9 h-9 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:border-gray-300 dark:hover:border-white/20 transition-all duration-200 bg-gray-100 dark:bg-white/5"
    @click="toggle"
  >
    <Sun v-if="isDark" :size="16" :stroke-width="2" />
    <Moon v-else :size="16" :stroke-width="2" />
  </button>
</template>

<script setup lang="ts">
import { Sun, Moon } from 'lucide-vue-next'
const { isDark, toggle } = useAppColorMode()
</script>
