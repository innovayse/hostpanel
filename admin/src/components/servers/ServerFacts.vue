<script setup lang="ts">
/**
 * The compact facts grid of a server card: an icon, a caption and a value for the group, datacenter, active services, their
 * monthly revenue, how the API is reached and the nameservers (one per line); a fact the server has no data for is not drawn.
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
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

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
  <!-- The card's width, not the screen's, decides one column or two: the grid counts its columns by this container -->
  <UiStack class="@container">
  <UiGrid v-if="server === null" :columns="2" :gap="UiGap.Sm" container aria-hidden="true" data-testid="server-facts-loading">
    <UiStack v-for="slot in SERVER_FACT_SLOTS" :key="slot" :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm">
      <UiSkeletonBar class="h-4 w-4 shrink-0 rounded" />
      <UiStack :gap="UiGap.Xs" class="min-w-0 flex-1">
        <UiSkeletonBar class="h-3 w-12 rounded" />
        <UiSkeletonBar class="h-3.5 w-20 rounded-md" />
      </UiStack>
    </UiStack>
  </UiGrid>
  <UiGrid v-else :columns="2" :gap="UiGap.Sm" container :data-testid="`server-facts-${server.id}`">
    <!-- One row shape for every fact: the icon sits on the label's line, the label and the values are one line each and cut with a full-text hint, so a long caption or host name never wraps or breaks inside a word -->
    <UiStack v-for="fact in facts" :key="fact.key" :direction="UiDirection.Row" :align="UiAlignItems.Start" :gap="UiGap.Sm" :data-testid="`server-fact-${fact.key}`">
      <UiIcon :markup="fact.icon" :size="UiSize.Sm" class="mt-0.5 shrink-0 text-text-muted" />
      <UiStack :gap="UiGap.None" class="min-w-0 flex-1">
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" truncate :title="fact.label">{{ fact.label }}</UiText>
        <UiText v-for="value in fact.values" :key="value" :tone="UiTextTone.Primary" :size="UiTextSize.Sm" truncate class="font-medium" :title="value">{{ value }}</UiText>
      </UiStack>
    </UiStack>
  </UiGrid>
  </UiStack>
</template>
