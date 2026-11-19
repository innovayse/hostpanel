<script setup lang="ts">
/**
 * SSL monitoring report: the SSL status of every domain, grouped by expiry. A row of chips counts the
 * domains of each group (the summary of the schema), only the groups that hold domains get a table section, and one
 * empty state stands in for the table when none does. The header holds the inactive-domains switch and the button
 * that checks the certificates again. The title, the two header controls, the table columns and the group heading
 * rows are the ones of its schema, which the skeleton draws too.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportGroupRow from '@/components/reports/ReportGroupRow.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { useReport } from '@/composables/useReport'
import { EMPTY_VALUE, formatDate, formatDateTime } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { reportPath } from '@/lib/reportsQuery'
import { useReportsStore } from '@/stores/reports'
import { SSL_GROUP_TITLE_KEYS, SSL_GROUP_TONES } from '@/types/reportssl'
import type { ReportSsl, ReportSslGroup, ReportSslRow } from '@/types/reportssl'
import type { ReportTableData } from '@/types/reporttabledata'
import { UiTone } from '@/utils/ui/tone'
import schema from '@/views/SslMonitoringView.schema'

const { t } = useI18n()

const store = useReportsStore()

/** Whether inactive domains are listed too. */
const includeInactive = ref(false)

const { data, load } = useReport<ReportSsl>(() =>
  reportPath('/reports/ssl-monitoring', { includeInactive: includeInactive.value }),
)

// Switching the list reloads at once.
watch(includeInactive, load)

/** The groups that hold domains: the table draws a section for each, and none for an empty one. */
const filledGroups = computed<ReportSslGroup[]>(() => (data.value?.groups ?? []).filter((group) => group.rows.length > 0))

/** Whether no group holds a domain, so the table shows its one empty state. */
const allEmpty = computed<boolean>(() => filledGroups.value.length === 0)

/** The table: the sections of the groups are its body, so it gives no rows of its own. */
const table = computed<ReportTableData<ReportSslRow>>(() => ({ empty: allEmpty.value }))

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
  <ReportPage :retry="load" :schema="schema" :table="table">
    <template #actions>
      <UiStack direction="row" gap="md" align="center" class="shrink-0 whitespace-nowrap">
        <UiTooltip :text="t('reports.sslMonitoring.showInactiveHint')" placement="bottom">
          <UiStack direction="row" gap="sm" align="center">
            <UiToggle
              v-model="includeInactive"
              :aria-label="t('reports.sslMonitoring.showInactive')"
              data-testid="report-toggle-inactive"
            />
            <UiText as="span" size="sm">
              {{ t('reports.sslMonitoring.showInactive') }}
            </UiText>
          </UiStack>
        </UiTooltip>
        <UiTooltip :text="t('reports.sslMonitoring.recheckHint')" placement="bottom">
          <UiButton :icon="Icon.Refresh" variant="secondary" :loading="store.updating" data-testid="report-revalidate" @click="revalidate">{{ t('reports.sslMonitoring.recheck') }}
          </UiButton>
        </UiTooltip>
      </UiStack>
    </template>

    <template #summary>
      <UiStack
        v-if="data && !allEmpty"
        direction="row"
        gap="sm"
        wrap
        role="group"
        :aria-label="t('reports.sslMonitoring.summary')"
        data-testid="ssl-summary"
      >
        <UiBadge v-for="group in data.groups" :key="group.group" :tone="SSL_GROUP_TONES[group.group]" data-testid="ssl-summary-chip">
          {{ t(SSL_GROUP_TITLE_KEYS[group.group]) }}: {{ group.rows.length }}
        </UiBadge>
      </UiStack>
    </template>

    <template #body="{ columns }">
      <template v-for="group in filledGroups" :key="group.group">
        <ReportGroupRow :label="t(SSL_GROUP_TITLE_KEYS[group.group])" :colspan="columns.length" />
        <UiTableRow v-for="row in group.rows" :key="row.domainName" hoverable>
          <UiTableCell>{{ row.domainName }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="row.hasSsl ? UiTone.Success : UiTone.Danger" small>
              {{ row.hasSsl ? t('reports.sslMonitoring.valid') : t('reports.sslMonitoring.noSsl') }}
            </UiBadge>
          </UiTableCell>
          <UiTableCell>{{ row.issuer ?? EMPTY_VALUE }}</UiTableCell>
          <UiTableCell>{{ formatDate(row.expiresAt) }}</UiTableCell>
          <UiTableCell>{{ formatDateTime(row.lastUpdate) }}</UiTableCell>
        </UiTableRow>
      </template>
    </template>
  </ReportPage>
</template>
