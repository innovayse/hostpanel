<template>
  <nav
    v-motion
    :initial="{ opacity: 0, y: -10 }"
    :enter="{ opacity: 1, y: 0, transition: { duration: 400 } }"
    class="flex flex-wrap items-center gap-1.5 sm:gap-2 text-xs sm:text-sm text-gray-500"
    :class="containerClass"
    aria-label="Breadcrumb"
  >
    <NuxtLink
      :to="localePath('/')"
      class="hover:text-primary-400 transition-colors whitespace-nowrap"
    >
      {{ $t('common.home') }}
    </NuxtLink>

    <template v-for="(item, index) in items" :key="index">
      <ChevronRight :size="14" :stroke-width="2" class="flex-shrink-0" />

      <NuxtLink
        v-if="item.to && index < items.length - 1"
        :to="item.to"
        class="hover:text-primary-400 transition-colors break-words max-w-[150px] sm:max-w-none truncate sm:whitespace-normal"
      >
        {{ item.label }}
      </NuxtLink>

      <span
        v-else
        :class="[
          index === items.length - 1 ? 'text-white font-medium' : '',
          'break-words max-w-[150px] sm:max-w-none truncate sm:whitespace-normal'
        ]"
      >
        {{ item.label }}
      </span>
    </template>
  </nav>
</template>

<script setup lang="ts">
/**
 * Reusable Breadcrumbs component
 * Used for navigation breadcrumbs across all pages
 */

import { ChevronRight } from 'lucide-vue-next'

interface BreadcrumbItem {
  label: string
  to?: string
}

interface Props {
  items: BreadcrumbItem[]
  containerClass?: string
}

withDefaults(defineProps<Props>(), {
  containerClass: 'mb-8'
})

const localePath = useLocalePath()
</script>
