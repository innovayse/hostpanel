<script setup lang="ts">
/**
 * Activity log of the current client: a read-only audit trail of the admin actions on the profile, with date,
 * description, user and IP address. A search box (admin name or email, description, IP address) and the filter dialog
 * (the period) with its removable chip narrow it. The search text, the period and the page live in the address
 * (`useListQuery`), so a reload, a shared link and the back button restore the same log, and the API narrows the
 * entries before it pages. A refused load shows the sentence with a Retry.
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientLogStore } from '@/stores/clientLog'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { useListQuery } from '@/composables/useListQuery'
import { usePeriodFilter } from '@/composables/usePeriodFilter'
import { formatDateTimeCompact, EMPTY_VALUE } from '@/lib/format'
import type { ActivityLogFilters } from '@/types/activitylog'
import UiCard from '@/components/ui/UiCard.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** The filter fields kept in the address: the first and the last day of the period. */
const FilterKey = {
  From: 'from',
  To: 'to',
} as const

/** Current route, source of the client id. */
const route = useRoute()

/** Client log store, the only caller of the activity-log endpoint. */
const store = useClientLogStore()

/** The period, the search text and the page of the log, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, loadOnChange, dateOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The applied period and its chips. */
const period = usePeriodFilter({ dateOf }, { from: FilterKey.From, to: FilterKey.To })

/** What the address narrows the log by, as the store sends it to the API. */
const filters = computed<ActivityLogFilters>(() => ({
  search: appliedSearch.value,
  from: period.from.value,
  to: period.to.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(period.chipSources)

/** The period of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(draft, FilterKey.From, FilterKey.To)

/** True while the first load is in flight and there are no entries yet; the table then draws its placeholder rows. */
const initialLoading = computed<boolean>(() => store.loading && store.logs.length === 0)

/** Client ID from route params. */
const clientId = computed(() => Number(route.params.id))

/**
 * Loads the page of the log the address names with the search and the period it holds.
 *
 * @returns Promise that resolves when data is loaded.
 */
const load = (): Promise<void> => store.fetchLogs(clientId.value, page.value, filters.value)

// Every change of the search, the period or the page loads the log the address names.
loadOnChange(load)
</script>

<template>
  <UiStack>
    <UiSectionHeader :level="1" variant="card">
      {{ t('clients.clientLog.activityLog') }}
    </UiSectionHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="client-log-search"
      :search-placeholder="t('clients.clientLog.searchLog')"
      filter-testid="client-log-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiField :label="t('shared.dateRange')">
        <UiDateRangePicker
          v-model="draftRange"
          :placeholder="t('shared.selectDateRange')"
          data-testid="client-log-filter-period"
        />
      </UiField>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="client-log-error"
      @retry="load"
    />

    <UiCard v-else class="overflow-x-auto">
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        :empty="!initialLoading && store.logs.length === 0"
        data-testid="client-log-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.clientLog.logEntry') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.clientLog.user') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.ipAddress') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="log in store.logs" :key="log.id" hoverable>
            <UiTableCell muted>{{ formatDateTimeCompact(log.createdAt) }}</UiTableCell>
            <UiTableCell>{{ log.description }}</UiTableCell>
            <UiTableCell>
              <UiStack gap="none">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ log.adminName ?? EMPTY_VALUE }}</UiText>
                <UiText v-if="log.adminEmail" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ log.adminEmail }}</UiText>
              </UiStack>
            </UiTableCell>
            <UiTableCell muted>{{ log.ipAddress ?? EMPTY_VALUE }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('shared.noRecordsFound') }}
        </template>
      </UiTable>
      <UiStack
        v-if="initialLoading || store.totalCount > 0"
        direction="row"
        :justify="UiJustify.Between"
        :align="UiAlignItems.Center"
      >
        <UiResultCount :loading="initialLoading" :size="UiTextSize.Sm" data-testid="client-log-count">
          {{ t('clients.clientLog.totalcountEntrValue', { totalCount: store.totalCount }) }}
        </UiResultCount>
        <UiPagination
          :page="page"
          :total="store.totalCount"
          :page-size="store.pageSize"
          :loading="initialLoading"
          testid="client-log-pagination"
          @update:page="setPage"
        />
      </UiStack>
    </UiCard>
  </UiStack>
</template>
