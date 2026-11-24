<script setup lang="ts">
/**
 * The e-mails sent to the current client: a read-only log with the send date, the subject and the outcome of each. A
 * search box (subject or recipient) and the filter dialog (the outcome and the period) with their removable chips
 * narrow it. The search text, the filters and the page live in the address (`useListQuery`), so a reload, a shared
 * link and the back button restore the same log, and the API narrows the entries before it pages. A refused load
 * shows the sentence with a Retry.
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientEmailStore } from '@/stores/clientEmail'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { useListQuery } from '@/composables/useListQuery'
import { usePeriodFilter } from '@/composables/usePeriodFilter'
import { EMAIL_LOG_OUTCOME_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { emailOutcomeLabelKey, successOfOutcome } from '@/lib/emailLogStatus'
import { statusLabel } from '@/lib/tones'
import { EmailLogOutcome } from '@/types/emaillog'
import type { EmailLogFilters } from '@/types/emaillog'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ClientEmailsView.schema'
import { UiHeadingVariant } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** The filter fields kept in the address: the outcome and the first and the last day of the period. */
const FilterKey = {
  Outcome: 'outcome',
  From: 'from',
  To: 'to',
} as const

/** Current route, source of the client id. */
const route = useRoute()

/** Client email store, the only caller of the email-log endpoint. */
const store = useClientEmailStore()

/** The outcome, the period, the search text and the page of the log, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, loadOnChange, enumOf, dateOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The outcome in the address as a filter choice, undefined when none is set or the value is not one. */
const outcomeOfAddress = enumOf(FilterKey.Outcome, EmailLogOutcome)

/** The applied period and its chips. */
const period = usePeriodFilter({ dateOf }, { from: FilterKey.From, to: FilterKey.To })

/** What the address narrows the log by, as the store sends it to the API. */
const filters = computed<EmailLogFilters>(() => ({
  search: appliedSearch.value,
  success: successOfOutcome(outcomeOfAddress.value),
  from: period.from.value,
  to: period.to.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Outcome,
    label: t('clients.clientEmails.status'),
    value: outcomeOfAddress.value === undefined ? undefined : statusLabel(EMAIL_LOG_OUTCOME_OPTIONS, outcomeOfAddress.value),
  },
  ...period.chipSources(),
])

/** Outcome choices; the empty value means any. */
const outcomeOptions = computed(() => [{ value: '', label: t('shared.any') }, ...EMAIL_LOG_OUTCOME_OPTIONS])

/** The period of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(draft, FilterKey.From, FilterKey.To)

/** Numeric client ID from route params. */
const clientId = computed(() => Number(route.params.id))

/**
 * Loads the page of the log the address names with the search and the filters it holds.
 *
 * @returns Promise that resolves when data is loaded.
 */
const load = (): Promise<void> => store.fetchClientEmails(clientId.value, page.value, filters.value)

// Every change of a filter, the search or the page loads the log the address names.
loadOnChange(load)
</script>

<template>
  <UiStack>
    <UiHeading :level="1" :variant="UiHeadingVariant.Card">{{ t('clients.clientEmails.emails') }}</UiHeading>

    <UiListToolbar
      v-model:search="search"
      search-testid="client-emails-search"
      :search-placeholder="t('clients.clientEmails.searchEmails')"
      filter-testid="client-emails-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack>
        <UiField :label="t('clients.clientEmails.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.outcome"
              :options="outcomeOptions"
              :placeholder="t('shared.any')"
              data-testid="client-emails-filter-outcome"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.dateRange')">
          <UiDateRangePicker
            v-model="draftRange"
            :placeholder="t('shared.selectDateRange')"
            data-testid="client-emails-filter-period"
          />
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="client-emails-error"
      @retry="load"
    />

    <UiDataTable
      v-else
      :empty-text="t('shared.noRecordsFound')"
      :density="UiTableDensity.Comfortable"
      :loading="store.loading && store.emails.length === 0"
      :empty="!store.loading && store.emails.length === 0"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="client-emails-pagination"
      data-testid="client-emails-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow v-for="email in store.emails" :key="email.id" hoverable>
          <UiTableCell muted>{{ formatDate(email.sentAt) }}</UiTableCell>
          <UiTableCell>{{ email.subject }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="email.success ? UiTone.Success : UiTone.Danger" :data-testid="`client-emails-outcome-${email.id}`">
              {{ t(emailOutcomeLabelKey(email)) }}
            </UiBadge>
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
    </UiDataTable>
  </UiStack>
</template>
