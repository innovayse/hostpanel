<script setup lang="ts">
/**
 * Quotes list: every client's quotes, paged by the server, with a search box and a filter dialog (client, stage,
 * validity) whose applied filters show as chips. The search text (the subject or the quote number), the filters and
 * the page live in the address (`useListQuery`), so a reload or a shared link shows the same list, and the API narrows
 * the list before it pages. The client's name is not searched, which only the Clients module can read: the client
 * filter picks the client instead.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import QuoteFilterFields from '@/components/billing/QuoteFilterFields.vue'
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
import { useListQuery } from '@/composables/useListQuery'
import { QuoteFilterKey, useQuoteFilters } from '@/composables/useQuoteFilters'
import { Icon } from '@/lib/icons'
import { quoteStageTone, statusLabel } from '@/lib/tones'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { useQuotesStore } from '@/stores/quotes'
import type { QuoteFilters } from '@/types/quote'
import { AppRoute, quotePath } from '@/lib/routes'

const { t } = useI18n()

/** The filter fields kept in the address: the quote filters every quote list has, and the client this list adds. */
const FilterKey = {
  ...QuoteFilterKey,
  ClientId: 'clientId',
} as const

const store = useQuotesStore()
const { clients, load: loadClients } = useClientOptions()

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, loadOnChange, enumOf, idOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The applied stage and validity, and their chips. */
const quoteFilters = useQuoteFilters({ enumOf })

/** The client in the address as an id, undefined when none is set or the value is not an id. */
const clientIdOfAddress = idOf(FilterKey.ClientId)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<QuoteFilters>(() => ({
  search: appliedSearch.value,
  clientId: clientIdOfAddress.value,
  ...quoteFilters.filters.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.ClientId,
    label: t('shared.client'),
    value: clientIdOfAddress.value === undefined
      ? undefined
      : (clients.value.find(client => client.id === clientIdOfAddress.value)?.name ?? String(clientIdOfAddress.value)),
  },
  ...quoteFilters.chipSources(),
])

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns Resolves when the request has settled.
 */
const load = (): Promise<void> => store.fetchAll(page.value, filters.value)

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

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
      v-model:search="search"
      search-testid="billing-quotes-search"
      :search-placeholder="t('billing.quotesList.searchQuotes')"
      filter-testid="billing-quotes-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack gap="md">
        <UiField :label="t('shared.client')">
          <UiClientSelect
            v-model="draft.clientId"
            :clients="clients"
            :placeholder="t('shared.startTypingToSearchClients')"
          />
        </UiField>
        <QuoteFilterFields
          v-model:stage="draft.stage"
          v-model:validity="draft.validity"
          testid-prefix="billing-quotes-filter"
        />
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
        :empty="!store.loading && store.quotes.length === 0"
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
          <UiTableRow v-for="quote in store.quotes" :key="quote.id" hoverable>
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
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="billing-quotes-pagination"
      @update:page="setPage"
    />
  </UiStack>
</template>
