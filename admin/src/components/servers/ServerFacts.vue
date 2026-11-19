<script setup lang="ts">
/**
 * The compact facts grid of a server card: an icon, a caption and a value for the group, datacenter, active services, their
 * monthly revenue, how the API is reached and the nameservers; a fact the server has no data for is not drawn.
 *
 * While `server` is null the grid draws {@link SERVER_FACT_SLOTS} placeholder facts of the same shape, so the card keeps its
 * height until the list has arrived (the card skeleton of the servers screen draws the same blocks).
 */
import { computed } from 'vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { SERVER_FACT_SLOTS, serverFacts } from '@/lib/servers'
import type { ServerFact } from '@/lib/servers'
import type { ServerDto } from '@/types/server'

/** Props for ServerFacts. */
const props = withDefaults(defineProps<{
  /** The server whose facts are shown; null while the list is still loading. */
  server?: ServerDto | null
}>(), {
  server: null,
})

/** The facts of the server, in display order; empty while loading. */
const facts = computed<ServerFact[]>(() => (props.server === null ? [] : serverFacts(props.server)))
</script>

<template>
  <UiGrid v-if="server === null" :columns="2" gap="sm" aria-hidden="true" data-testid="server-facts-loading">
    <UiStack v-for="slot in SERVER_FACT_SLOTS" :key="slot" direction="row" align="center" gap="sm">
      <UiSkeletonBar class="h-4 w-4 shrink-0 rounded" />
      <UiStack gap="xs" class="min-w-0 flex-1">
        <UiSkeletonBar class="h-3 w-12 rounded" />
        <UiSkeletonBar class="h-3.5 w-20 rounded-md" />
      </UiStack>
    </UiStack>
  </UiGrid>
  <UiGrid v-else :columns="2" gap="sm" :data-testid="`server-facts-${server.id}`">
    <UiStack v-for="fact in facts" :key="fact.key" direction="row" align="center" gap="sm" :data-testid="`server-fact-${fact.key}`">
      <UiIcon :markup="fact.icon" size="sm" class="text-text-muted" />
      <UiStack gap="none" class="min-w-0">
        <UiText tone="muted" size="xs">{{ fact.label }}</UiText>
        <UiText tone="primary" size="sm" class="break-words font-medium">{{ fact.value }}</UiText>
      </UiStack>
    </UiStack>
  </UiGrid>
</template>
