<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { REPORTS } from '../reports'
import { REPORT_ICONS } from '../reportIcons'

const route = useRoute()
const expanded = ref(false)

function isActive(slug: string): boolean {
  return route.path === `/reports/${slug}`
}

function iconFor(name: string): string {
  return REPORT_ICONS[name] ?? REPORT_ICONS['bar-chart']
}
</script>

<template>
  <div
    class="flex flex-col bg-surface-card border-r border-border transition-all duration-200 ease-in-out overflow-y-auto overflow-x-hidden shrink-0"
    :class="expanded ? 'w-[230px]' : 'w-[48px]'"
    @mouseenter="expanded = true"
    @mouseleave="expanded = false"
  >
    <nav class="flex flex-col gap-0.5 pt-2 pb-4">
      <RouterLink
        v-for="r in REPORTS"
        :key="r.slug"
        :to="`/reports/${r.slug}`"
        class="relative flex items-center gap-2.5 px-3 py-2.5 transition-all duration-150 no-underline"
        :class="isActive(r.slug)
          ? 'text-primary-400 bg-primary-500/8'
          : 'text-text-muted hover:text-text-secondary hover:bg-white/[0.04]'"
      >
        <span v-if="isActive(r.slug)" class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand" />

        <!-- Per-report icon -->
        <svg class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" v-html="iconFor(r.icon)" />

        <span
          class="text-[0.78rem] font-medium whitespace-nowrap transition-opacity duration-200"
          :class="expanded ? 'opacity-100' : 'opacity-0'"
        >
          {{ r.label }}
        </span>
      </RouterLink>
    </nav>
  </div>
</template>
