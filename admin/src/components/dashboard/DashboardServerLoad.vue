<script setup lang="ts">
/**
 * Server capacity card of the dashboard: one bar per server showing the share of its account capacity
 * in use. A server with unlimited capacity or no connection test yet has no share and is left out. The
 * title row stays while the servers load.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiProgress from '@/components/ui/UiProgress.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { serverLoadPercent } from '@/lib/servers'
import type { ServerOptionDto } from '@/types/server'
import { UiCardPadding, UiCardRadius } from '@/utils/ui/card'
import { UiGap } from '@/utils/ui/layout'
import { UiHeadingVariant } from '@/utils/ui/text'

/** One bar of the card. */
interface ServerLoadRow {
  /** The server. */
  server: ServerOptionDto
  /** Share of its capacity in use, in percent. */
  percent: number
}

/** Props for DashboardServerLoad. */
const props = defineProps<{
  /** Servers with their capacity and last test result. */
  servers: ServerOptionDto[]
  /** Whether the servers are still loading. */
  loading: boolean
}>()

const { t } = useI18n()

/** Servers that have a load share, most loaded first. */
const rows = computed<ServerLoadRow[]>(() =>
  props.servers
    .map((server): ServerLoadRow | null => {
      const percent = serverLoadPercent(server)
      return percent === null ? null : { server, percent }
    })
    .filter((row): row is ServerLoadRow => row !== null)
    .sort((a, b) => b.percent - a.percent),
)
</script>

<template>
  <UiCard :padding="UiCardPadding.None" :radius="UiCardRadius.Panel" class="p-5.5" data-testid="dashboard-server-load">
    <UiStack :gap="UiGap.Md">
      <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ t('dashboard.serverLoad.title') }}</UiHeading>
      <UiProgress v-if="loading && rows.length === 0" :value="0" :label="t('dashboard.serverLoad.title')" loading />
      <UiEmptyState
        v-else-if="rows.length === 0"
        :title="t('dashboard.serverLoad.emptyTitle')"
        :description="t('dashboard.serverLoad.emptyDescription')"
        data-testid="dashboard-server-load-empty"
      />
      <UiProgress
        v-for="row in rows"
        :key="row.server.id"
        :value="row.percent"
        :label="row.server.name"
        :data-testid="`dashboard-server-load-${row.server.id}`"
      />
    </UiStack>
  </UiCard>
</template>
