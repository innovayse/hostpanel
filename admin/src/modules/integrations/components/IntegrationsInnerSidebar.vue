<script setup lang="ts">
/**
 * Collapsible inner sidebar for the Integrations section.
 * Expands on hover to show category labels, collapses to icon-only on mouse leave.
 */
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { INTEGRATION_CATEGORIES } from '../types/integration.meta'

const route = useRoute()
const expanded = ref(false)

/** SVG path data for each category icon. */
const CATEGORY_ICONS: Record<string, string> = {
  payments: '<rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/>',
  registrars: '<circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/>',
  provisioning: '<rect x="2" y="3" width="20" height="14" rx="2"/><line x1="8" y1="21" x2="16" y2="21"/><line x1="12" y1="17" x2="12" y2="21"/>',
  email: '<path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/>',
  fraud: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>',
  migration: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/>',
}

/** All sidebar categories (Migration Tools moved to main sidebar). */
const SIDEBAR_CATEGORIES = [...INTEGRATION_CATEGORIES]

function isActive(key: string): boolean {
  return route.path === `/integrations/${key}` || (key === 'payments' && route.path === '/integrations')
}
</script>

<template>
  <div
    class="flex flex-col bg-surface-card border-r border-border transition-all duration-200 ease-in-out overflow-y-auto overflow-x-hidden shrink-0"
    :class="expanded ? 'w-[220px]' : 'w-[48px]'"
    @mouseenter="expanded = true"
    @mouseleave="expanded = false"
  >
    <nav class="flex flex-col gap-0.5 pt-2 pb-4">
      <RouterLink
        v-for="cat in SIDEBAR_CATEGORIES"
        :key="cat.key"
        :to="`/integrations/${cat.key}`"
        class="relative flex items-center gap-2.5 px-3 py-2.5 transition-all duration-150 no-underline"
        :class="isActive(cat.key)
          ? 'text-primary-400 bg-primary-500/8'
          : 'text-text-muted hover:text-text-secondary hover:bg-white/[0.04]'"
      >
        <!-- Active indicator -->
        <span
          v-if="isActive(cat.key)"
          class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand"
        />

        <!-- Icon -->
        <svg
          class="w-5 h-5 shrink-0"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
          v-html="CATEGORY_ICONS[cat.key] ?? CATEGORY_ICONS.fraud"
        />

        <!-- Label -->
        <span
          class="text-[0.78rem] font-medium whitespace-nowrap transition-opacity duration-200 flex-1"
          :class="expanded ? 'opacity-100' : 'opacity-0'"
        >
          {{ cat.label }}
        </span>
      </RouterLink>
    </nav>
  </div>
</template>
