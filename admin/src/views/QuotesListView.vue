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
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import schema from '@/views/QuotesListView.schema'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { useListQuery } from '@/composables/useListQuery'
import { QuoteFilterKey, useQuoteFilters } from '@/composables/useQuoteFilters'
import { quoteStageTone, statusLabel } from '@/lib/tones'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { useQuotesStore } from '@/stores/quotes'
import type { QuoteFilters } from '@/types/quote'
import { AppRoute, quotePath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiGap } from '@/utils/ui/layout'
import { UiActionKind } from '@/utils/ui/actions'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  headerAction(UiActionKind.Add, { label: t('billing.addQuote.createQuote'), variant: UiButtonVariant.Primary, testid: 'billing-quotes-create', to: AppRoute.BillingQuotesAdd }),
])

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
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="billing-quotes-search"
      :search-placeholder="t('billing.quotesList.searchAllQuotes')"
      filter-testid="billing-quotes-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack :gap="UiGap.Md">
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

    <UiDataTable
      :loading="store.loading && store.quotes.length === 0"
      :empty="!store.loading && store.quotes.length === 0"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="billing-quotes-pagination"
      data-testid="billing-quotes-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow v-for="quote in store.quotes" :key="quote.id" hoverable>
          <UiTableCell muted>
            <UiLink :to="quotePath(quote.id)" :variant="UiButtonVariant.Link" :data-testid="`billing-quote-open-${quote.id}`">
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
    </UiDataTable>
  </UiStack>
</template>
