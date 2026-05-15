<script setup lang="ts">
/**
 * Displays a single integration as a card inside a category section.
 *
 * Shows logo color block, name, Plugin badge if applicable,
 * description, active/inactive status, and Configure link.
 */
import { RouterLink } from 'vue-router'
import { INTEGRATION_META } from '../types/integration.meta'
import type { IntegrationDto, IntegrationSlug } from '../types/integration.types'

/** Props for IntegrationRow. */
const props = defineProps<{
  /** The integration summary data from the store. */
  integration: IntegrationDto
}>()

/** Static metadata for this integration (color, slug). */
const meta = INTEGRATION_META[props.integration.slug as IntegrationSlug]
</script>

<template>
  <div
    class="flex items-center gap-3 px-4 py-4 transition-colors duration-150 hover:bg-white/[0.02] group"
    :class="{ 'opacity-50': !integration.isEnabled }"
  >
    <!-- Color logo block -->
    <div
      class="w-9 h-9 rounded-lg flex-shrink-0 flex items-center justify-center text-white text-xs font-bold font-display"
      :class="meta?.color ?? 'bg-text-muted'"
    />

    <!-- Name + description -->
    <div class="flex-1 min-w-0">
      <div class="flex items-center gap-1.5 mb-0.5">
        <span class="text-[0.84rem] font-semibold text-text-primary truncate">{{ integration.name }}</span>
        <span
          v-if="integration.isPlugin"
          class="shrink-0 text-[0.62rem] font-semibold text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-full px-1.5 py-px"
        >
          Plugin
        </span>
      </div>
      <div class="text-[0.75rem] text-text-muted truncate">{{ integration.description }}</div>
    </div>

    <!-- Status + configure -->
    <div class="flex flex-col items-end gap-1.5 shrink-0">
      <span
        class="text-[0.65rem] font-semibold rounded-full px-2 py-0.5"
        :class="integration.isEnabled
          ? 'text-status-green bg-status-green/10 border border-status-green/20'
          : 'text-text-muted bg-white/[0.04] border border-border'"
      >
        {{ integration.isEnabled ? 'Active' : 'Inactive' }}
      </span>
      <RouterLink
        :to="`/integrations/${integration.slug}`"
        class="text-[0.72rem] font-medium text-primary-400 hover:text-primary-300 transition-colors no-underline"
      >
        Configure →
      </RouterLink>
    </div>
  </div>
</template>
