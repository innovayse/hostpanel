<script setup lang="ts">
/**
 * Gateway transaction log: the payment-gateway interactions the API records (settled invoice transactions and open
 * payment sessions), narrowed by a search box (gateway name or debug text) and by the filter dialog (the result and the
 * period, given as days) with its removable chips, and paged by the server. The search text, the filters and the page
 * live in the address (`useListQuery`), so a reload, a shared link and the back button restore the same log. A refused
 * load shows the sentence with a Retry.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/GatewayLogView.schema'
import UiText from '@/components/ui/UiText.vue'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { useListQuery } from '@/composables/useListQuery'
import { usePeriodFilter } from '@/composables/usePeriodFilter'
import { GATEWAY_LOG_RESULT_OPTIONS } from '@/lib/constants'
import { gatewayLogResultTone, statusLabel } from '@/lib/tones'
import { schemaHeader } from '@/lib/schemaHeader'
import { useGatewayLogStore } from '@/stores/gatewayLog'
import { GatewayLogResult } from '@/types/gatewaylog'
import type { GatewayLogFilters } from '@/types/gatewaylog'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

const { t } = useI18n()

/** The filter fields kept in the address: the result and the first and the last day of the period. */
const FilterKey = {
  Result: 'result',
  From: 'from',
  To: 'to',
} as const

const store = useGatewayLogStore()

/** The result, the period, the search text and the page of the log, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, loadOnChange, enumOf, dateOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The result in the address as a gateway log result, undefined when none is set or the value is not one. */
const resultOfAddress = enumOf(FilterKey.Result, GatewayLogResult)

/** The applied period and its chips. */
const period = usePeriodFilter({ dateOf }, { from: FilterKey.From, to: FilterKey.To })

/** What the address narrows the log by, as the store sends it to the API. */
const filters = computed<GatewayLogFilters>(() => ({
  search: appliedSearch.value,
  result: resultOfAddress.value,
  from: period.from.value,
  to: period.to.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Result,
    label: t('billing.gatewayLog.result'),
    value: resultOfAddress.value === undefined ? undefined : statusLabel(GATEWAY_LOG_RESULT_OPTIONS, resultOfAddress.value),
  },
  ...period.chipSources(),
])

/** Result choices; the empty value means any. */
const resultOptions = computed(() => [{ value: '', label: t('shared.any') }, ...GATEWAY_LOG_RESULT_OPTIONS])

/** The period of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(draft, FilterKey.From, FilterKey.To)

/**
 * Loads the page of the log the address names with the search and the filters it holds.
 *
 * @returns Promise that resolves when the page is loaded.
 */
const load = (): Promise<void> => store.fetchPage(page.value, filters.value)

// Every change of a filter, the search or the page loads the log the address names.
loadOnChange(load)
</script>

<template>
  <UiStack data-testid="billing-gateway-log-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <UiListToolbar
      v-model:search="search"
      search-testid="billing-gateway-log-search"
      :search-placeholder="t('billing.gatewayLog.searchPlaceholder')"
      filter-testid="billing-gateway-log-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack>
        <UiField :label="t('billing.gatewayLog.result')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.result"
              :options="resultOptions"
              :placeholder="t('shared.any')"
              data-testid="billing-gateway-log-filter-result"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.dateRange')">
          <UiDateRangePicker
            v-model="draftRange"
            :placeholder="t('shared.selectDateRange')"
            data-testid="billing-gateway-log-filter-period"
          />
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="billing-gateway-log-error"
      @retry="load"
    />

    <UiDataTable
      :empty-text="t('shared.noRecordsFound')"
      :loading="store.loading && store.entries.length === 0"
      :empty="!store.loading && store.entries.length === 0"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="billing-gateway-log-pagination"
      data-testid="billing-gateway-log-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow v-for="entry in store.entries" :key="entry.id" hoverable>
          <UiTableCell muted>{{ new Date(entry.dateTime).toLocaleString() }}</UiTableCell>
          <UiTableCell>{{ entry.gateway }}</UiTableCell>
          <UiTableCell>
            <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm" class="whitespace-pre-wrap break-words font-mono">{{ entry.debugData }}</UiText>
          </UiTableCell>
          <UiTableCell>
            <UiBadge :tone="gatewayLogResultTone(entry.result)">{{ statusLabel(GATEWAY_LOG_RESULT_OPTIONS, entry.result) }}</UiBadge>
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
    </UiDataTable>
  </UiStack>
</template>
