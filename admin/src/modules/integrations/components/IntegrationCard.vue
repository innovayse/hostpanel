<script setup lang="ts">
/**
 * Displays a single integration as a clickable card with logo, title, description, and status.
 *
 * Clicking the card navigates to the integration's configuration page.
 */
import { RouterLink } from 'vue-router'
import { INTEGRATION_META } from '../types/integration.meta'
import type { IntegrationDto, IntegrationSlug } from '../types/integration.types'

/** Props for IntegrationCard. */
const props = defineProps<{
  /** The integration summary data from the store. */
  integration: IntegrationDto 
}>()

/** Static metadata for this integration (logo, color, description). */
const meta = INTEGRATION_META[props.integration.slug as IntegrationSlug]
</script>

<template>
  <RouterLink
    :to="`/integrations/${integration.slug}`"
    class="group relative flex flex-col bg-surface-card border border-border rounded-xl p-6 transition-all duration-200 hover:border-primary-500/40 hover:shadow-lg hover:shadow-primary-500/5 no-underline cursor-pointer"
  >
    <!-- Status badge -->
    <span
      class="absolute top-4 right-4 text-[0.65rem] font-semibold rounded-full px-2.5 py-1"
      :class="integration.isEnabled
        ? 'text-status-green bg-status-green/10 border border-status-green/20'
        : 'text-text-muted bg-white/[0.04] border border-border'"
    >
      {{ integration.isEnabled ? 'Active' : 'Inactive' }}
    </span>

    <!-- Logo -->
    <div class="w-12 h-12 rounded-xl flex items-center justify-center mb-5 p-2.5" :class="meta?.color ?? 'bg-text-muted'">
      <img
        v-if="meta?.logo"
        :src="meta.logo"
        :alt="integration.name"
        class="w-full h-full object-contain integration-logo"
      />
    </div>

    <!-- Title row -->
    <div class="flex items-center gap-2 mb-2">
      <h3 class="text-[0.95rem] font-semibold text-text-primary leading-tight">{{ integration.name }}</h3>
      <span
        v-if="integration.isPlugin"
        class="shrink-0 text-[0.6rem] font-semibold text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-full px-1.5 py-px"
      >
        Plugin
      </span>
    </div>

    <!-- Description -->
    <p class="text-[0.8rem] leading-relaxed text-text-muted flex-1">
      {{ meta?.shortDescription ?? integration.description }}
    </p>

    <!-- Configure link -->
    <div class="mt-5 pt-3.5 border-t border-border/50">
      <span class="text-[0.78rem] font-medium text-primary-400 group-hover:text-primary-300 transition-colors">
        Configure →
      </span>
    </div>
  </RouterLink>
</template>

<style scoped>
.integration-logo {
  filter: brightness(0) invert(1);
}
</style>
