<script setup lang="ts">
/**
 * Disk space and bandwidth report: the use of every hosting account, per server, as last fetched
 * from the servers. "Update now" fetches fresh figures: the tables show their skeleton meanwhile, and a server the
 * refresh could not read is named in the notice of the schema (its placeholder holds the place while the report loads),
 * never left out silently. With no figures at all one empty state carries the Update now action. The report has one table
 * per server, so the screen draws them itself in the default slot of the page.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportEmptyState from '@/components/reports/ReportEmptyState.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { formatDateTime } from '@/lib/format'
import { reportColumns } from '@/lib/reportsSchema'
import { useReportsStore } from '@/stores/reports'
import type { ReportColumn } from '@/types/reportcolumn'
import { DiskUsageFailureReason } from '@/types/reportdiskusage'
import type { ReportDiskUsage, ReportDiskUsageFailure, ReportDiskUsageServer } from '@/types/reportdiskusage'
import { UiTone } from '@/utils/ui/tone'
import schema from '@/views/DiskUsageView.schema'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

const store = useReportsStore()

const { data, load } = useReport<ReportDiskUsage>(() => '/reports/disk-usage')

/** Percent of a limit from which an account is flagged as filling up. */
const WARN_PERCENT = 70

/** Percent of a limit from which an account is flagged as nearly full. */
const DANGER_PERCENT = 90

/** The servers with their accounts; none until the first load. */
const servers = computed<ReportDiskUsageServer[]>(() => data.value?.servers ?? [])

/** Servers the last refresh could not read. */
const failures = computed<ReportDiskUsageFailure[]>(() => data.value?.failures ?? [])

/** The sentence of each reason a server gave no figures, by locale key. */
const FAILURE_TEXT_KEYS: Readonly<Record<DiskUsageFailureReason, string>> = {
  [DiskUsageFailureReason.UnsupportedModule]: 'reports.diskUsage.failure.unsupportedModule',
  [DiskUsageFailureReason.MissingCredentials]: 'reports.diskUsage.failure.missingCredentials',
  [DiskUsageFailureReason.Unreachable]: 'reports.diskUsage.failure.unreachable',
}

/** Table columns: the captions and alignment of the schema, the figures drawn as numbers. */
const columns = computed<ReportColumn[]>(() =>
  reportColumns(schema.table, t, {
    diskUsage: { numeric: true },
    diskLimit: { numeric: true },
    diskPercent: { numeric: true },
    bwUsage: { numeric: true },
    bwLimit: { numeric: true },
    bwPercent: { numeric: true },
  }),
)

/** When the figures were last fetched, formatted; null when never. */
const lastUpdated = computed<string | null>(() => (data.value?.lastUpdated ? formatDateTime(data.value.lastUpdated) : null))

/**
 * Picks the tone of a used-percent badge.
 *
 * @param percent - Share of the limit in use.
 * @returns Danger when nearly full, warning when filling, neutral otherwise.
 */
const percentTone = (percent: number): UiTone => {
  if (percent >= DANGER_PERCENT) {
    return UiTone.Danger
  }
  return percent >= WARN_PERCENT ? UiTone.Warning : UiTone.Neutral
}

/** Asks the servers for fresh figures and shows them. */
const updateNow = async (): Promise<void> => {
  const result = await store.refreshReport<ReportDiskUsage>('/reports/disk-usage/update')
  if (result !== null) {
    data.value = result
  }
}
</script>

<template>
  <ReportPage :retry="load" :schema="schema">
    <template #alert>
      <UiAlert v-if="data && failures.length > 0" variant="warning" data-testid="report-failures">
        <UiText size="sm">
          {{ t('reports.diskUsage.failuresTitle') }}
        </UiText>
        <UiText v-for="failure in failures" :key="failure.serverName" size="sm" data-testid="report-failure">
          {{ failure.serverName }}: {{ t(FAILURE_TEXT_KEYS[failure.reason]) }}
        </UiText>
      </UiAlert>
    </template>

    <template v-if="data">
      <UiStack v-if="servers.length > 0" direction="row" justify="between" align="center">
        <UiText v-if="lastUpdated" tone="muted" size="sm" data-testid="report-last-updated">
          {{ t('reports.diskUsage.diskSpaceUsageStatsLast', { value: lastUpdated }) }}
        </UiText>
        <UiButton :action="UiActionKind.Refresh" variant="secondary" :loading="store.updating" data-testid="report-update-now" @click="updateNow">
          {{ t('reports.diskUsage.updateNow') }}
        </UiButton>
      </UiStack>

      <ReportTable v-else-if="store.updating" :columns="columns" row-key="domain" />
      <ReportEmptyState v-else :title="t('reports.diskUsage.noData')" :hint="t('reports.diskUsage.noDataHint')">
        <template #actions>
          <UiButton :action="UiActionKind.Refresh" variant="secondary" data-testid="report-update-now" @click="updateNow">
            {{ t('reports.diskUsage.updateNow') }}
          </UiButton>
        </template>
      </ReportEmptyState>

      <UiStack v-for="server in servers" :key="server.serverName" gap="sm">
        <UiHeading :level="2" variant="section">
          {{ server.serverName }}
        </UiHeading>
        <ReportTable
          :columns="columns"
          :rows="server.rows"
          row-key="domain"
          :empty-text="t('reports.diskUsage.noAccountsOnThisServer')"
        >
          <template #cell-clientName="{ row }">
            {{ row.clientName }} · {{ row.domain }}
          </template>
          <template #cell-diskPercent="{ row }">
            <UiBadge :tone="percentTone(row.diskPercent)" small>
              {{ row.diskPercent }}%
            </UiBadge>
          </template>
          <template #cell-bwPercent="{ row }">
            <UiBadge :tone="percentTone(row.bwPercent)" small>
              {{ row.bwPercent }}%
            </UiBadge>
          </template>
        </ReportTable>
      </UiStack>
    </template>
    <!-- Until the first answer the one table of the schema stands in for the table of every server. -->
    <ReportTable v-else :columns="columns" :empty="false" />
  </ReportPage>
</template>
