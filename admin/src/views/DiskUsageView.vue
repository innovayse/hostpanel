<script setup lang="ts">
/**
 * Disk space and bandwidth report: the use of every hosting account, per server, as last fetched
 * from the servers. "Update now" fetches fresh figures.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { useReportsStore } from '@/stores/reports'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportDiskUsage } from '@/types/reportdiskusage'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const store = useReportsStore()

const { data, load } = useReport<ReportDiskUsage>(() => '/reports/disk-usage')

/** Percent of a limit from which an account is flagged as filling up. */
const WARN_PERCENT = 70

/** Percent of a limit from which an account is flagged as nearly full. */
const DANGER_PERCENT = 90

/** Table columns. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'clientName', label: t('reports.diskUsage.clientNameDomain') },
  { key: 'diskUsage', label: t('reports.diskUsage.diskUsage'), numeric: true },
  { key: 'diskLimit', label: t('reports.diskUsage.diskLimit'), numeric: true },
  { key: 'diskPercent', label: t('reports.diskUsage.used'), numeric: true },
  { key: 'bwUsage', label: t('reports.diskUsage.bwUsage'), numeric: true },
  { key: 'bwLimit', label: t('reports.diskUsage.bwLimit'), numeric: true },
  { key: 'bwPercent', label: t('reports.diskUsage.used'), numeric: true },
])

/** When the figures were last fetched, formatted; null when never. */
const lastUpdated = computed<string | null>(() =>
  data.value?.lastUpdated
    ? new Date(data.value.lastUpdated).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' })
    : null,
)

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
  <ReportPage :retry="load" :title="t('reports.diskUsage.diskSpaceBandwidthUsageSummary')" :description="t('reports.diskUsage.thisReportShowsTheDisk')">
    <template #filters>
      <UiStack direction="row" justify="between" align="center" class="mb-6">
        <UiText v-if="lastUpdated" tone="muted" size="sm" data-testid="report-last-updated">
          {{ t('reports.diskUsage.diskSpaceUsageStatsLast', { value: lastUpdated }) }}
        </UiText>
        <UiText v-else tone="muted" size="sm">
          {{ t('reports.diskUsage.noDataYetClickUpdate') }}
        </UiText>
        <UiButton variant="secondary" :loading="store.updating" data-testid="report-update-now" @click="updateNow">
          {{ store.updating ? t('reports.diskUsage.updating') : t('reports.diskUsage.updateNow') }}
        </UiButton>
      </UiStack>
    </template>

    <template v-if="data">
      <UiCard v-if="data.servers.length === 0">
        <UiEmptyState :title="t('reports.diskUsage.noData')" />
      </UiCard>
      <UiStack v-for="server in data.servers" :key="server.serverName" gap="sm">
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
  </ReportPage>
</template>
