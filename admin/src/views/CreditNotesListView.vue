<script setup lang="ts">
/**
 * Credit notes: every credit note issued against a paid invoice, newest first, with a search box (number, reason, refund
 * reference or id) and a filter dialog (client, status and method) whose applied filters show as chips. The search text, the
 * filters and the page live in the address (`useListQuery`), so a reload or a shared link shows the same list, and the API
 * applies them before it pages. The client is chosen, not typed: only the Clients module can read client names.
 *
 * A credit note is not an invoice credit: it is a numbered document that reverses part of an invoice that was already paid, as
 * account credit or as a gateway refund. Each row names the note, its invoice and client, how its value went back and its
 * total, and opens the note's page. Notes are issued from the Credit notes tab of the invoice. Reads `stores/creditNotes`.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import CreditNoteStatusBadge from '@/components/billing/CreditNoteStatusBadge.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiField from '@/components/ui/UiField.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { useListQuery } from '@/composables/useListQuery'
import { formatDateTime } from '@/lib/format'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { formatPlainAmount } from '@/lib/money'
import { clientPath, creditNotePath, invoicePath } from '@/lib/routes'
import { useCreditNotesStore } from '@/stores/creditNotes'
import { CreditNoteMethod, CreditNoteStatus } from '@/types/creditnote'

const { t } = useI18n()

/** Store holding the notes of the current page and the load state. */
const store = useCreditNotesStore()

/** The filter fields kept in the address, beside the search text and the page. */
const FilterKey = {
  ClientId: 'clientId',
  Status: 'status',
  Method: 'method',
} as const

/** One of the {@link FilterKey} values. */
type FilterKey = (typeof FilterKey)[keyof typeof FilterKey]

/** The filters, the search text and the page of the list, as the address holds them. */
const list = useListQuery<FilterKey>(Object.values(FilterKey))

/** The applied client as an id, undefined when none is set or the address holds something that is not an id. */
const clientId = list.idOf(FilterKey.ClientId)

/** The applied status, undefined when none is set or the address holds a value that is not a status. */
const status = list.enumOf(FilterKey.Status, CreditNoteStatus)

/** The applied method, undefined when none is set or the address holds a value that is not a method. */
const method = list.enumOf(FilterKey.Method, CreditNoteMethod)

/** The clients the client filter offers, and the call that loads them. */
const { clients, load: loadClients } = useClientOptions()

/** The sentence of the last refused load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** Options of the status filter; the empty value means any. */
const statusOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(CreditNoteStatus).map((value) => ({ value: value as string, label: t(`billing.creditNotes.status.${value}`) })),
])

/** Options of the method filter; the empty value means any. */
const methodOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(CreditNoteMethod).map((value) => ({ value: value as string, label: t(`billing.creditNotes.method.${value}`) })),
])

/** One chip per applied filter, in the order they are shown. */
const chips = list.chipsOf(() => [
  {
    key: FilterKey.ClientId,
    label: t('shared.client'),
    value: clientId.value === undefined
      ? undefined
      : (clients.value.find(client => client.id === clientId.value)?.name ?? String(clientId.value)),
  },
  {
    key: FilterKey.Status,
    label: t('shared.status'),
    value: status.value === undefined ? undefined : t(`billing.creditNotes.status.${status.value}`),
  },
  {
    key: FilterKey.Method,
    label: t('billing.creditNotes.methodLabel'),
    value: method.value === undefined ? undefined : t(`billing.creditNotes.method.${method.value}`),
  },
])

/**
 * Loads the page the address names, with the search text and filters it holds.
 *
 * @returns Promise that resolves when the page has settled.
 */
const load = async (): Promise<void> => {
  await store.fetchPage(
    { search: list.appliedSearch.value, clientId: clientId.value, status: status.value, method: method.value },
    list.page.value,
    DEFAULT_PAGE_SIZE,
  )
  loadError.value = store.error
}

// The address is the source of truth: the first load and every change of a filter, the search or the page read the list.
list.loadOnChange(load)

onMounted(loadClients)
</script>

<template>
  <UiStack data-testid="billing-credit-notes-view">
    <UiPageHeader :title="t('billing.creditNotes.heading')" :subtitle="t('billing.creditNotes.subtitle')" />

    <UiAlert variant="info" data-testid="billing-credit-notes-explain">{{ t('billing.creditNotes.explain') }}</UiAlert>

    <UiErrorState v-if="loadError !== null" :message="loadError" :loading="store.loading" testid="billing-credit-notes-error" @retry="load" />

    <UiListToolbar
      v-model:search="list.search.value"
      search-testid="billing-credit-notes-search"
      :search-placeholder="t('billing.creditNotes.searchCreditNotes')"
      filter-testid="billing-credit-notes-filter"
      :active-count="list.activeCount.value"
      :chips="chips"
      @apply="list.apply"
      @reset="list.reset"
      @cancel="list.cancel"
      @remove="list.remove"
      @clear="list.clearAll"
    >
      <UiStack gap="md">
        <UiField :label="t('shared.client')">
          <UiClientSelect
            v-model="list.draft.value.clientId"
            :clients="clients"
            :placeholder="t('shared.startTypingToSearchClients')"
            data-testid="billing-credit-notes-filter-client"
          />
        </UiField>
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="list.draft.value.status"
              :options="statusOptions"
              :placeholder="t('shared.any')"
              data-testid="billing-credit-notes-filter-status"
            />
          </template>
        </UiField>
        <UiField :label="t('billing.creditNotes.methodLabel')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="list.draft.value.method"
              :options="methodOptions"
              :placeholder="t('shared.any')"
              data-testid="billing-credit-notes-filter-method"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiResultCount v-if="loadError === null" :loading="store.loading && store.notes.length === 0" data-testid="billing-credit-notes-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiResultCount>

    <UiCard>
      <UiTable
        :loading="store.loading && store.notes.length === 0"
        :empty="!store.loading && loadError === null && store.notes.length === 0"
        data-testid="billing-credit-notes-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('billing.creditNotes.number') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.invoice') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.client') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.statusLabel') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.methodLabel') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('billing.creditNotes.total') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.issuedAt') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="note in store.notes" :key="note.id" hoverable>
            <UiTableCell>
              <UiLink :to="creditNotePath(note.id)" variant="link" :data-testid="`billing-credit-note-${note.id}`">
                {{ note.number ?? `#${note.id}` }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiLink :to="invoicePath(note.invoiceId)" variant="link">#{{ note.invoiceId }}</UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiLink :to="clientPath(note.clientId)" variant="link">{{ note.clientName }}</UiLink>
            </UiTableCell>
            <UiTableCell><CreditNoteStatusBadge :status="note.status" /></UiTableCell>
            <UiTableCell>{{ t(`billing.creditNotes.method.${note.method}`) }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(note.total, note.currency) }}</UiTableCell>
            <UiTableCell muted>{{ formatDateTime(note.issuedAt) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('billing.creditNotes.empty') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="list.page.value"
      :total="store.totalCount"
      :page-size="DEFAULT_PAGE_SIZE"
      testid="billing-credit-notes-pagination"
      @update:page="list.setPage"
    />
  </UiStack>
</template>
