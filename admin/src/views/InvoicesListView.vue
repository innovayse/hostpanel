<script setup lang="ts">
/**
 * Billing invoices list: every client's invoices, with a paid, due and overdue summary of the page, a search box
 * (invoice number or payment method), a filter dialog (client, status and invoice date) whose applied filters show as
 * chips, paging, bulk actions on the selected rows (`useRowSelection`, the floating `InvoiceBulkBar`) and a confirmed
 * delete. An invoice number opens the invoice as a real link, and so does a click anywhere else on the row. The filters, the search text and the page live in the address (`useListQuery`), so a reload or a shared link
 * shows the same list, and the API applies them before it pages. The client is chosen, not typed: only the Clients module
 * can read client names, so the list cannot search them. Deleting and cancelling are confirmed first; every action
 * reports its outcome (success, or the API's sentence) above the table, and a refused load shows the sentence with a Retry.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import InvoiceBulkBar from '@/components/billing/InvoiceBulkBar.vue'
import InvoiceFilterFields from '@/components/billing/InvoiceFilterFields.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { InvoiceFilterKey, useInvoiceFilters } from '@/composables/useInvoiceFilters'
import { useListQuery } from '@/composables/useListQuery'
import { useRowSelection } from '@/composables/useRowSelection'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import schema from '@/views/InvoicesListView.schema'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { anchorOf } from '@/lib/anchor'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { bucketAmountText, summarizeInvoices } from '@/lib/invoices'
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'
import { invoiceStatusTone, statusLabel } from '@/lib/tones'
import { formatPlainAmount } from '@/lib/money'
import { useBillingStore } from '@/stores/billing'
import type { InvoiceBulkAction } from '@/types/invoice'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { invoiceEditPath, invoiceViewPath } from '@/lib/routes'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiGap } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'
import { schemaHeader } from '@/lib/schemaHeader'

const { t } = useI18n()

const store = useBillingStore()

/** Router, which a click on a row uses to open the invoice. */
const router = useRouter()

/** The filter fields kept in the address, beside the search text and the page: the shared invoice filters and the client. */
const FilterKey = {
  ...InvoiceFilterKey,
  ClientId: 'clientId',
} as const

/** One of the {@link FilterKey} values. */
type FilterKey = (typeof FilterKey)[keyof typeof FilterKey]

/** The filters, the search text and the page of the list, as the address holds them. */
const list = useListQuery<FilterKey>(Object.values(FilterKey))

/** The applied status and period, and their chips. */
const invoiceFilters = useInvoiceFilters(list)

/** The applied client as an id, undefined when none is set or the address holds something that is not an id. */
const clientId = list.idOf(FilterKey.ClientId)

/** The clients the client filter offers, and the call that loads them. */
const { clients, load: loadClients } = useClientOptions()

/** Invoice waiting for the delete confirmation, null when none. */
const pendingDeleteId = ref<number | null>(null)

/** True while a bulk action or a delete is in flight. */
const processing = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last action. */
const feedback = useOutcomeToast()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/** The invoice-date range of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(list.draft, FilterKey.From, FilterKey.To)

/** One chip per applied filter, in the order they are shown: the client first, then the status and the period. */
const chips = list.chipsOf(() => [
  {
    key: FilterKey.ClientId,
    label: t('shared.client'),
    value: clientId.value === undefined
      ? undefined
      : (clients.value.find(client => client.id === clientId.value)?.name ?? String(clientId.value)),
  },
  ...invoiceFilters.chipSources(),
])

/** The paid, due and overdue rollup of the loaded page. */
const summary = computed(() => summarizeInvoices(store.invoices))

/** The selected invoices of the shown rows. */
const selection = useRowSelection(() => store.invoices.map((invoice) => invoice.id))

/**
 * Loads the page the address names, with the search text and filters it holds; the selection of the previous page is dropped.
 *
 * @returns Resolves when the request has settled.
 */
const load = async (): Promise<void> => {
  selection.clear()
  await store.fetchAll(list.page.value, { ...invoiceFilters.filters.value, clientId: clientId.value, search: list.appliedSearch.value })
  loadError.value = store.error
}

/**
 * Runs one invoice command, shows its outcome and reloads the page on success.
 *
 * @param command - The store call to run.
 * @param successKey - Locale key of the success sentence.
 * @returns Resolves when the request (and the reload) has settled.
 */
const runCommand = async (command: () => Promise<boolean>, successKey: string): Promise<void> => {
  processing.value = true
  const ok = await command()
  processing.value = false
  if (ok) {
    feedback.show(t(successKey), false)
    await load()
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/**
 * Applies a bulk action to the selected invoices, shows the outcome and reloads the page.
 *
 * @param action - What to do with them.
 * @returns Resolves when the request (and the reload) has settled.
 */
const runBulk = (action: InvoiceBulkAction): Promise<void> =>
  runCommand(() => store.bulkAction([...selection.ids.value], action), 'billing.invoicesList.bulkActionDone')

/**
 * Deletes the invoice that waits for confirmation, shows the outcome and reloads the page.
 *
 * @returns Resolves when the request (and the reload) has settled.
 */
const confirmDelete = async (): Promise<void> => {
  const id = pendingDeleteId.value
  if (id === null) {
    return
  }
  await runCommand(() => store.deleteInvoice(id), 'billing.invoicesList.invoiceDeleted')
  pendingDeleteId.value = null
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!processing.value) {
    pendingDeleteId.value = null
  }
}

/**
 * The row actions of an invoice: view and edit as real links, and delete last (confirmed first).
 *
 * @param id - The invoice id.
 * @returns The action list passed to UiRowActions.
 */
const invoiceActions = (id: number): UiMenuAction[] => [
  {
    label: t('shared.view'),
    icon: Icon.Eye,
    testid: `billing-invoice-view-${id}`,
    to: invoiceViewPath(id),
  },
  {
    label: t('shared.edit'),
    icon: Icon.Edit,
    testid: `billing-invoice-edit-${id}`,
    to: invoiceEditPath(id),
  },
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    testid: `billing-invoice-delete-${id}`,
    danger: true,
    /**
     * Asks for confirmation before the invoice is deleted.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      pendingDeleteId.value = id
    },
  },
]

/**
 * Opens an invoice's detail page when a click lands on the row itself; a click on a link inside the row (the invoice
 * number) is left to that link, so a modifier or middle click still opens a new tab and nothing navigates twice.
 *
 * @param id - The invoice id.
 * @param event - The click on the row.
 * @returns Resolves when the navigation has settled.
 */
const openInvoice = async (id: number, event: MouseEvent): Promise<void> => {
  if (anchorOf(event.target) !== null) {
    return
  }
  await router.push(invoiceViewPath(id))
}

// The address is the source of truth: the first load and every change of a filter, the search or the page read the list.
list.loadOnChange(load)

onMounted(loadClients)
</script>

<template>
  <UiStack data-testid="billing-invoices-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)">
      <template #actions>
        <PageHelpLink :url="guideUrl" />
      </template>
    </UiPageHeader>

    <UiGrid :columns="3">
      <UiStatTile
        :label="t('clients.clientSummary.paid')"
        :value="bucketAmountText(summary.paid)"
        :hint="t('billing.invoicesList.summaryHint', { count: summary.paid.count }, summary.paid.count)"
        :tone="UiTone.Success"
        data-testid="billing-invoices-summary-paid"
      />
      <UiStatTile
        :label="t('billing.invoicesList.due')"
        :value="bucketAmountText(summary.due)"
        :hint="t('billing.invoicesList.summaryHint', { count: summary.due.count }, summary.due.count)"
        :tone="UiTone.Warning"
        data-testid="billing-invoices-summary-due"
      />
      <UiStatTile
        :label="t('clients.clientSummary.overdue')"
        :value="bucketAmountText(summary.overdue)"
        :hint="t('billing.invoicesList.summaryHint', { count: summary.overdue.count }, summary.overdue.count)"
        :tone="UiTone.Danger"
        data-testid="billing-invoices-summary-overdue"
      />
    </UiGrid>

    <UiListToolbar
      v-model:search="list.search.value"
      search-testid="billing-invoices-search"
      filter-testid="billing-invoices-filter"
      :search-placeholder="t('billing.invoicesList.searchInvoices')"
      :active-count="list.activeCount.value"
      :chips="chips"
      @apply="list.apply"
      @reset="list.reset"
      @cancel="list.cancel"
      @remove="list.remove"
      @clear="list.clearAll"
    >
      <UiStack :gap="UiGap.Md">
        <UiField :label="t('shared.client')">
          <UiClientSelect
            v-model="list.draft.value.clientId"
            :clients="clients"
            :placeholder="t('shared.startTypingToSearchClients')"
            data-testid="billing-invoices-filter-client"
          />
        </UiField>
        <InvoiceFilterFields
          v-model:status="list.draft.value.status"
          v-model:period="draftRange"
          testid-prefix="billing-invoices-filter"
        />
      </UiStack>
    </UiListToolbar>


    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-invoices-error"
      @retry="load"
    />

    <UiDataTable
      :loading="store.loading && store.invoices.length === 0"
      :empty="!store.loading && store.invoices.length === 0"
      :scrollable="false"
      :selection="selection"
      select-all-testid="billing-invoices-select-all"
      :page="list.page.value"
      :total="store.totalCount"
      :page-size="LEDGER_PAGE_SIZE"
      pagination-testid="billing-invoices-pagination"
      data-testid="billing-invoices-table"
      @update:page="list.setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="invoice in store.invoices"
          :key="invoice.id"
          clickable
          :row-id="invoice.id"
          :select-testid="`billing-invoice-select-${invoice.id}`"
          :data-testid="`billing-invoice-row-${invoice.id}`"
          @click="openInvoice(invoice.id, $event)"
        >
          <UiTableCell>
            <UiLink :variant="UiButtonVariant.Link" :to="invoiceViewPath(invoice.id)" :data-testid="`billing-invoice-open-${invoice.id}`">
              #{{ invoice.id }}
            </UiLink>
          </UiTableCell>
          <UiTableCell hide-below="sm">
            <UiText class="truncate">{{ invoice.clientName }}</UiText>
          </UiTableCell>
          <UiTableCell muted hide-below="md">{{ formatDate(invoice.createdAt) }}</UiTableCell>
          <UiTableCell muted hide-below="2xl">{{ formatDate(invoice.dueDate) }}</UiTableCell>
          <UiTableCell numeric hide-below="sm">{{ formatPlainAmount(invoice.total, invoice.currency) }}</UiTableCell>
          <UiTableCell muted hide-below="2xl">{{ invoice.paymentMethod || EMPTY_VALUE }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="invoiceStatusTone(invoice.status)">{{ statusLabel(INVOICE_STATUS_OPTIONS, invoice.status) }}</UiBadge>
          </UiTableCell>
          <UiTableCell actions @click.stop>
            <UiRowActions :actions="invoiceActions(invoice.id)" :testid="`billing-invoice-actions-${invoice.id}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState :title="t('shared.noInvoicesFound')" data-testid="billing-invoices-empty" />
      </template>
    </UiDataTable>

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="pendingDeleteId !== null"
      :title="t('billing.invoicesList.deleteInvoice')"
      :message="t('billing.invoicesList.deleteInvoiceMessage', { id: pendingDeleteId ?? 0 })"
      :confirm-label="t('shared.delete')"
      :loading="processing"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />

    <InvoiceBulkBar
      :count="selection.count.value"
      :perform="runBulk"
      testid="billing-invoices-bulk"
      @clear="selection.clear"
    />
  </UiStack>
</template>
