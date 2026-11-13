<script setup lang="ts">
/**
 * SSL monitoring report: the SSL status of every domain, grouped, with a switch for inactive
 * domains and a button that re-validates the certificates.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportGroupRow from '@/components/reports/ReportGroupRow.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useReport } from '@/composables/useReport'
import { reportPath } from '@/lib/reportsQuery'
import { useReportsStore } from '@/stores/reports'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportSsl } from '@/types/reportssl'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const store = useReportsStore()

/** Whether inactive domains are listed too. */
const includeInactive = ref(false)

const { data, load } = useReport<ReportSsl>(() =>
  reportPath('/reports/ssl-monitoring', { includeInactive: includeInactive.value }),
)

// Switching the list reloads at once.
watch(includeInactive, load)

/** Table columns. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'domainName', label: t('shared.domainName') },
  { key: 'hasSsl', label: t('shared.status') },
  { key: 'issuer', label: t('reports.sslMonitoring.issuer') },
  { key: 'expiresAt', label: t('reports.sslMonitoring.expiry') },
  { key: 'lastUpdate', label: t('reports.sslMonitoring.lastUpdate') },
])

/** Flips whether inactive domains are listed. */
const toggleInactive = (): void => {
  includeInactive.value = !includeInactive.value
}

/** Asks the API to check the certificates again and shows the fresh result. */
const revalidate = async (): Promise<void> => {
  const result = await store.refreshReport<ReportSsl>(
    reportPath('/reports/ssl-monitoring/revalidate', { includeInactive: includeInactive.value }),
  )
  if (result !== null) {
    data.value = result
  }
}
</script>

<template>
  <ReportPage :retry="load" :title="t('reports.sslMonitoring.sslCertificateMonitoring')" :description="t('reports.sslMonitoring.displaysAListOfDomains')">
    <template #filters>
      <UiStack direction="row" gap="sm" class="mb-6">
        <UiButton
          :variant="includeInactive ? 'primary' : 'secondary'"
          data-testid="report-toggle-inactive"
          @click="toggleInactive"
        >
          {{ includeInactive ? t('reports.sslMonitoring.hideInactiveDomains') : t('reports.sslMonitoring.showInactiveDomains') }}
        </UiButton>
        <UiButton variant="secondary" :loading="store.updating" data-testid="report-revalidate" @click="revalidate">
          {{ t('reports.sslMonitoring.reValidateSslStatus') }}
        </UiButton>
      </UiStack>
    </template>

    <ReportTable v-if="data" :columns="columns" :empty="data.groups.length === 0">
      <template #body>
        <template v-for="group in data.groups" :key="group.groupName">
          <ReportGroupRow :label="group.groupName" :colspan="columns.length" />
          <UiTableRow v-if="group.rows.length === 0">
            <UiTableCell :colspan="columns.length" muted>
              {{ t('shared.noRecordsFound') }}
            </UiTableCell>
          </UiTableRow>
          <UiTableRow v-for="row in group.rows" :key="row.domainName" hoverable>
            <UiTableCell>{{ row.domainName }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="row.hasSsl ? UiTone.Success : UiTone.Danger" small>
                {{ row.hasSsl ? t('reports.sslMonitoring.valid') : t('reports.sslMonitoring.noSsl') }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell>{{ row.issuer ?? '—' }}</UiTableCell>
            <UiTableCell>{{ row.expiresAt ?? '—' }}</UiTableCell>
            <UiTableCell>{{ row.lastUpdate }}</UiTableCell>
          </UiTableRow>
        </template>
      </template>
    </ReportTable>
  </ReportPage>
</template>
