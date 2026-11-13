<script setup lang="ts">
/**
 * Domain renewal emails report: the renewal reminders that were sent, filtered by client,
 * registrar, domain and date range.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportField from '@/components/reports/ReportField.vue'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiInput from '@/components/ui/UiInput.vue'
import { useReport } from '@/composables/useReport'
import { useReportClients } from '@/composables/useReportClients'
import { reportPath } from '@/lib/reportsQuery'
import { renewalEmailKey } from '@/lib/reportsRows'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import { DOMAIN_REMINDER_TYPE_LABEL_KEYS } from '@/types/domain'
import type { ReportDomainRenewal } from '@/types/reportdomainrenewal'
import { UiAlign } from '@/utils/ui/table'
import { UiTone } from '@/utils/ui/tone'

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

/** Table columns. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'clientName', label: t('shared.client') },
  { key: 'domain', label: t('shared.domain') },
  { key: 'sentAt', label: t('reports.domainRenewalEmails.dateSent'), format: ReportCellFormat.Date },
  { key: 'reminder', label: t('reports.domainRenewalEmails.reminder') },
  { key: 'recipients', label: t('reports.domainRenewalEmails.recipients') },
  { key: 'sent', label: t('reports.domainRenewalEmails.sent'), align: UiAlign.Center },
])
</script>

<template>
  <ReportPage
    :title="t('reports.domainRenewalEmails.domainRenewalReminderEmails')"
    :description="t('reports.domainRenewalEmails.thisReportCanBeUsed')"
    :retry="load"
  >
    <template #filters>
      <ReportFilterBar :generate-label="t('shared.apply')" @generate="load">
        <template #fields>
          <ReportField :label="t('shared.client')" width="grow">
            <UiClientSelect v-model="selectedClient" :clients="clients" :placeholder="t('shared.startTypingToSearchClients')" />
          </ReportField>
          <ReportField :label="t('shared.registrar')">
            <UiInput v-model="registrar" :placeholder="t('shared.any')" data-testid="report-registrar" />
          </ReportField>
          <ReportField :label="t('shared.domain')">
            <UiInput v-model="domain" :placeholder="t('reports.domainRenewalEmails.optional')" data-testid="report-domain" />
          </ReportField>
          <ReportField :label="t('shared.dateRange')" width="grow">
            <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
          </ReportField>
        </template>
      </ReportFilterBar>
    </template>

    <ReportTable
      v-if="data"
      :columns="columns"
      :rows="data.rows"
      :row-key="renewalEmailKey"
      :empty-text="t('shared.noDataFoundForThis2')"
    >
      <template #cell-reminder="{ row }">
        {{ t(DOMAIN_REMINDER_TYPE_LABEL_KEYS[row.reminder]) }}
      </template>
      <template #cell-sent>
        <UiBadge :tone="UiTone.Success" small>
          {{ t('shared.yes') }}
        </UiBadge>
      </template>
    </ReportTable>
  </ReportPage>
</template>
