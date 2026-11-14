<script setup lang="ts">
/**
 * Quotes list: every client's quotes, paged by the server, with a search box and a filter dialog (stage, client,
 * validity) whose applied filters show as chips. The server pages the list; the search text and the filters narrow
 * the loaded page as the operator types. The filters, the search text and the page live in the address
 * (`useFilterQuery`), so a reload or a shared link shows the same list.
 */
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiCardRole } from '@/utils/ui/table'
import UiText from '@/components/ui/UiText.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { Icon } from '@/lib/icons'
import { quoteStageTone, statusLabel } from '@/lib/tones'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { useQuotesStore } from '@/stores/quotes'
import type { QuoteListItem } from '@/types/quote'
import { AppRoute, quotePath } from '@/lib/routes'
import type { UiFilterChip } from '@/utils/ui/filter'

const { t } = useI18n()

/** Whether a quote's validity date is still ahead; an empty filter value means either. */
const ValidityPeriod = {
  Valid: 'valid',
  Expired: 'expired',
} as const

/** The filter fields kept in the address, beside the search text and the page. */
const FilterKey = {
  Stage: 'stage',
  ClientId: 'clientId',
  Validity: 'validity',
} as const

/** One of the {@link FilterKey} values. */
type FilterKey = (typeof FilterKey)[keyof typeof FilterKey]

const store = useQuotesStore()
const { clients, load: loadClients } = useClientOptions()

/** The filters, the search text and the page of the list, as the address holds them. */
const filters = useFilterQuery<FilterKey>([FilterKey.Stage, FilterKey.ClientId, FilterKey.Validity])

/** Options of the validity select. */
const validityOptions = computed(() => [
  { value: '', label: t('shared.any') },
  { value: ValidityPeriod.Valid, label: t('billing.quotesList.valid') },
  { value: ValidityPeriod.Expired, label: t('billing.quotesList.expired') },
])

/** The applied filters as chips, in the order they are shown. */
const chips = computed<UiFilterChip<FilterKey>[]>(() => {
  const applied = filters.applied.value
  const result: UiFilterChip<FilterKey>[] = []
  if (applied.stage !== '') {
    result.push({ key: FilterKey.Stage, label: t('shared.stage'), value: statusLabel(QUOTE_STAGE_OPTIONS, applied.stage) })
  }
  if (applied.clientId !== '') {
    const name = clients.value.find(client => String(client.id) === applied.clientId)?.name ?? applied.clientId
    result.push({ key: FilterKey.ClientId, label: t('shared.client'), value: name })
  }
  if (applied.validity !== '') {
    result.push({ key: FilterKey.Validity, label: t('billing.quotesList.validityPeriod'), value: statusLabel(validityOptions.value, applied.validity) })
  }
  return result
})

/**
 * Tells whether a quote passes the search text and the applied filters.
 *
 * @param quote - A quote of the loaded page.
 * @returns True when the quote matches every filter that is set and contains the search text.
 */
const matches = (quote: QuoteListItem): boolean => {
  const applied = filters.applied.value
  const term = filters.search.value.trim().toLowerCase()
  if (term !== '' && ![String(quote.id), quote.subject, quote.clientName].some(field => field.toLowerCase().includes(term))) {
    return false
  }
  if (applied.clientId !== '' && String(quote.clientId) !== applied.clientId) {
    return false
  }
  if (applied.stage !== '' && quote.stage !== applied.stage) {
    return false
  }
  if (applied.validity !== '') {
    const expired = new Date(quote.validUntil) < new Date()
    return applied.validity === ValidityPeriod.Expired ? expired : !expired
  }
  return true
}

/** The loaded page, narrowed by the search text and the filters. */
const shownQuotes = computed<QuoteListItem[]>(() => store.quotes.filter(matches))

/**
 * Loads the page named by the address.
 *
 * @returns Resolves when the request has settled.
 */
const load = (): Promise<void> => store.fetchAll(filters.page.value)

// The address is the source of truth: the first load and every change of the page read the list.
watch(filters.page, load, { immediate: true })

onMounted(loadClients)
</script>

<template>
  <UiStack data-testid="billing-quotes-view">
    <UiPageHeader :title="t('shared.quotes')" :subtitle="t('billing.quotesList.customerQuotesAndProposals')">
      <template #actions>
        <UiLink :to="AppRoute.BillingQuotesAdd" variant="primary" data-testid="billing-quotes-create">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('billing.quotesList.createQuote') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiListToolbar
      v-model:search="filters.search.value"
      search-testid="billing-quotes-search"
      :active-count="filters.activeCount.value"
      :chips="chips"
      @apply="filters.apply"
      @reset="filters.reset"
      @cancel="filters.cancel"
      @remove="filters.remove"
      @clear="filters.clearAll"
    >
      <UiStack gap="md">
        <UiField :label="t('shared.client')">
          <UiClientSelect
            v-model="filters.draft.value.clientId"
            :clients="clients"
            :placeholder="t('shared.startTypingToSearchClients')"
          />
        </UiField>
        <UiField :label="t('shared.stage')">
          <UiSelect v-model="filters.draft.value.stage" :options="QUOTE_STAGE_OPTIONS" :placeholder="t('shared.stage')" />
        </UiField>
        <UiField :label="t('billing.quotesList.validityPeriod')">
          <UiSelect v-model="filters.draft.value.validity" :options="validityOptions" :placeholder="t('billing.quotesList.validityPeriod')" />
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="billing-quotes-error"
      @retry="load"
    />

    <UiText tone="muted" size="md" data-testid="billing-quotes-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && store.quotes.length === 0"
        :empty="!store.loading && shownQuotes.length === 0"
        data-testid="billing-quotes-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader :card-role="UiCardRole.Title">{{ t('shared.subject') }}</UiTableHeader>
            <UiTableHeader :card-role="UiCardRole.Badge">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.total') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.expiryDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.quotesList.created') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="quote in shownQuotes" :key="quote.id" hoverable>
            <UiTableCell muted>
              <UiLink :to="quotePath(quote.id)" variant="link" :data-testid="`billing-quote-open-${quote.id}`">
                {{ quote.id }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>{{ quote.clientName }}</UiTableCell>
            <UiTableCell>{{ quote.subject }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="quoteStageTone(quote.stage)">{{ statusLabel(QUOTE_STAGE_OPTIONS, quote.stage) }}</UiBadge>
            </UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(quote.total, quote.currency) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(quote.validUntil) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(quote.dateCreated) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState :title="t('billing.quotesList.noQuotesFound')" data-testid="billing-quotes-empty" />
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="filters.page.value"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="billing-quotes-pagination"
      @update:page="filters.setPage"
    />
  </UiStack>
</template>
