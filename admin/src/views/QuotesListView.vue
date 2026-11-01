<script setup lang="ts">
/**
 * Quotes list: every client's quotes, paged by the server and narrowed on the page by subject,
 * stage, client and validity as the operator types.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
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
import UiText from '@/components/ui/UiText.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { Icon } from '@/lib/icons'
import { quoteStageTone, statusLabel } from '@/lib/tones'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { useQuotesStore } from '@/stores/quotes'
import type { QuoteListItem } from '@/types/quote'
import { AppRoute, quotePath } from '@/lib/routes'

const { t } = useI18n()

/** Whether a quote's validity date is still ahead. */
const ValidityPeriod = {
  Any: 'any',
  Valid: 'valid',
  Expired: 'expired',
} as const

const store = useQuotesStore()
const { clients, load: loadClients } = useClientOptions()

/** Current page (1-based). */
const page = ref(1)

/** True while the filter card is open. */
const filtersOpen = ref(false)

/** Subject text the list is narrowed by. */
const filterSubject = ref('')

/** Stage the list is narrowed by; empty means every stage. */
const filterStage = ref('')

/** Client the list is narrowed by, as the picker holds the id; empty means every client. */
const filterClientId = ref('')

/** Validity the list is narrowed by. */
const filterValidity = ref<string>(ValidityPeriod.Any)

/** Options of the validity select. */
const validityOptions = computed(() => [
  { value: ValidityPeriod.Any, label: t('shared.any') },
  { value: ValidityPeriod.Valid, label: t('billing.quotesList.valid') },
  { value: ValidityPeriod.Expired, label: t('billing.quotesList.expired') },
])

/**
 * Tells whether a quote passes the filter card.
 *
 * @param quote - A quote of the loaded page.
 * @returns True when the quote matches every filter that is set.
 */
const matches = (quote: QuoteListItem): boolean => {
  if (filterClientId.value !== '' && String(quote.clientId) !== filterClientId.value) {
    return false
  }
  if (filterSubject.value !== '' && !quote.subject.toLowerCase().includes(filterSubject.value.toLowerCase())) {
    return false
  }
  if (filterStage.value !== '' && quote.stage !== filterStage.value) {
    return false
  }
  if (filterValidity.value !== ValidityPeriod.Any) {
    const expired = new Date(quote.validUntil) < new Date()
    return filterValidity.value === ValidityPeriod.Expired ? expired : !expired
  }
  return true
}

/** The loaded page, narrowed by the filter card. */
const shownQuotes = computed<QuoteListItem[]>(() => store.quotes.filter(matches))

/**
 * Loads a page of quotes.
 *
 * @param next - Page number (1-based).
 */
const load = async (next: number): Promise<void> => {
  page.value = next
  await store.fetchAll(next)
}

/** Clears every filter. */
const clearFilters = (): void => {
  filterSubject.value = ''
  filterStage.value = ''
  filterClientId.value = ''
  filterValidity.value = ValidityPeriod.Any
}

onMounted(() => {
  void load(1)
  void loadClients()
})
</script>

<template>
  <UiStack data-testid="billing-quotes-view">
    <UiPageHeader :title="t('shared.quotes')" :subtitle="t('billing.quotesList.customerQuotesAndProposals')">
      <template #actions>
        <UiButton variant="secondary" data-testid="billing-quotes-filter-toggle" @click="filtersOpen = !filtersOpen">
          {{ filtersOpen ? t('shared.hideFilters') : t('shared.searchFilter') }}
        </UiButton>
        <UiLink :to="AppRoute.BillingQuotesAdd" variant="primary" data-testid="billing-quotes-create">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('billing.quotesList.createQuote') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiFilterCard v-if="filtersOpen">
      <UiGrid :columns="2" container class="items-end">
        <UiField :label="t('shared.subject')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="filterSubject"
              type="text"
              :placeholder="t('shared.search')"
              data-testid="billing-quotes-filter-subject"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.client')">
          <UiClientSelect
            v-model="filterClientId"
            :clients="clients"
            :placeholder="t('shared.startTypingToSearchClients')"
          />
        </UiField>
        <UiField :label="t('shared.stage')">
          <UiSelect v-model="filterStage" :options="QUOTE_STAGE_OPTIONS" :placeholder="t('shared.stage')" />
        </UiField>
        <UiField :label="t('billing.quotesList.validityPeriod')">
          <UiSelect v-model="filterValidity" :options="validityOptions" :placeholder="t('billing.quotesList.validityPeriod')" />
        </UiField>
      </UiGrid>
      <template #actions>
        <UiStack direction="row" justify="center">
          <UiButton variant="ghost" data-testid="billing-quotes-clear" @click="clearFilters">
            {{ t('shared.clear') }}
          </UiButton>
        </UiStack>
      </template>
    </UiFilterCard>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="billing-quotes-error"
      @retry="load(page)"
    />

    <UiText tone="muted" size="md" data-testid="billing-quotes-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && store.quotes.length === 0"
        :empty="!store.loading && shownQuotes.length === 0"
        :colspan="7"
        data-testid="billing-quotes-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.subject') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.total') }}</UiTableHeader>
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
            <UiTableCell>{{ formatPlainAmount(quote.total, quote.currency) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(quote.validUntil) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(quote.dateCreated) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('billing.quotesList.noQuotesFound') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="billing-quotes-pagination"
      @update:page="load"
    />
  </UiStack>
</template>
