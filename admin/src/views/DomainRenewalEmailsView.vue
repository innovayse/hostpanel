<script setup lang="ts">
/**
 * Domain renewal emails report: the renewal reminders that were sent, filtered by client,
 * registrar, domain and date range.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiInput from '@/components/ui/UiInput.vue'
import { useReport } from '@/composables/useReport'
import { useReportClients } from '@/composables/useReportClients'
import { reportPath } from '@/lib/reportsQuery'
import { renewalEmailKey } from '@/lib/reportsRows'
import { ReportCellFormat } from '@/types/reportcolumn'
import { DOMAIN_REMINDER_TYPE_LABEL_KEYS } from '@/types/domain'
import type { ReportDomainRenewal, ReportDomainRenewalRow } from '@/types/reportdomainrenewal'
import type { ReportTableData } from '@/types/reporttabledata'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import { UiTone } from '@/utils/ui/tone'
import schema from '@/views/DomainRenewalEmailsView.schema'

const { t } = useI18n()

const { clients } = useReportClients()

/** The client chosen in the picker; empty for all clients. */
const selectedClient = ref<string | number | null>('')

/** Registrar to look for; empty for any. */
const registrar = ref('')

/** Domain to look for; empty for any. */
const domain = ref('')

/** The send dates to report; none for all. */
const dateRange = ref<[string, string] | null>(null)

const { data, load } = useReport<ReportDomainRenewal>(() =>
  reportPath('/reports/domain-renewal-emails', {
    clientId: selectedClient.value,
    registrar: registrar.value.trim(),
    domain: domain.value.trim(),
    from: dateRange.value?.[0],
    to: dateRange.value?.[1],
  }),
)

/** The client and the date range take the free width of the filter row. */
const WIDTHS = { client: ReportFieldWidth.Grow, dateRange: ReportFieldWidth.Grow } as const

/** The reminders that were sent, the send date written as a date; none until the report is read. */
const table = computed<ReportTableData<ReportDomainRenewalRow>>(() => ({
  rows: data.value?.rows ?? [],
  rowKey: renewalEmailKey,
  extras: { sentAt: { format: ReportCellFormat.Date } },
}))
</script>

<template>
  <ReportPage :schema="schema" :retry="load" :widths="WIDTHS" :generate-label="t('shared.apply')" :table="table" @generate="load">
    <template #field-client>
      <UiClientSelect v-model="selectedClient" :clients="clients" :placeholder="t('shared.allClients')" />
    </template>
    <template #field-registrar>
      <UiInput v-model="registrar" :placeholder="t('shared.any')" data-testid="report-registrar" />
    </template>
    <template #field-domain>
      <UiInput v-model="domain" :placeholder="t('shared.any')" data-testid="report-domain" />
    </template>
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>

    <template #cell-reminder="{ row }">
      {{ t(DOMAIN_REMINDER_TYPE_LABEL_KEYS[row.reminder]) }}
    </template>
    <template #cell-sent>
      <UiBadge :tone="UiTone.Success" small>
        {{ t('shared.yes') }}
      </UiBadge>
    </template>
  </ReportPage>
</template>
