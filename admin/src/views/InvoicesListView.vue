<script setup lang="ts">
/**
 * Billing invoices list: every client's invoices, with a paid, due and overdue summary of the page, a search box,
 * a filter dialog (status and invoice date) whose applied filters show as chips, paging, bulk actions on the
 * selected rows (`useRowSelection`, the floating `InvoiceBulkBar`) and a confirmed delete. The filters, the search text and the page live in the address
 * (`useFilterQuery`), so a reload or a shared link shows the same list. Deleting and cancelling are confirmed
 * first; every action reports its outcome (success, or the API's sentence) above the table, and a refused load
 * shows the sentence with a Retry.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import InvoiceBulkBar from '@/components/billing/InvoiceBulkBar.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { useRowSelection } from '@/composables/useRowSelection'
import { useResultBanner } from '@/composables/useResultBanner'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { bucketAmountText, searchInvoices, summarizeInvoices } from '@/lib/invoices'
import { invoiceStatusTone } from '@/lib/tones'
import { formatPlainAmount } from '@/lib/money'
import { useBillingStore } from '@/stores/billing'
import { pageFirstRow, pageLastRow } from '@/lib/paging'
import { InvoiceStatus } from '@/types/invoice'
import type { InvoiceBulkAction } from '@/types/invoice'
import type { Invoice } from '@/types/invoice'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { invoiceEditPath, invoiceViewPath } from '@/lib/routes'
import { UiTextSize } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import type { UiFilterChip } from '@/utils/ui/filter'

const { t } = useI18n()

const store = useBillingStore()

/** The filter fields kept in the address, beside the search text and the page. */
const FilterKey = {
  Status: 'status',
  From: 'from',
  To: 'to',
} as const

/** One of the {@link FilterKey} values. */
type FilterKey = (typeof FilterKey)[keyof typeof FilterKey]

/** The filters, the search text and the page of the list, as the address holds them. */
const filters = useFilterQuery<FilterKey>([FilterKey.Status, FilterKey.From, FilterKey.To])

/** Invoice waiting for the delete confirmation, null when none. */
const pendingDeleteId = ref<number | null>(null)

/** True while a bulk action or a delete is in flight. */
const processing = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last action. */
const feedback = useResultBanner()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** Options of the status filter: the shared lifecycle statuses plus the two collection states. */
const statusOptions = computed(() => [
  ...INVOICE_STATUS_OPTIONS,
  { value: InvoiceStatus.Collections, label: t('billing.invoicesList.collections') },
  { value: InvoiceStatus.PaymentPending, label: t('billing.invoicesList.paymentPending') },
])

/** The invoice-date range of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(filters.draft, FilterKey.From, FilterKey.To)

/** The applied filters as chips, in the order they are shown. */
const chips = computed<UiFilterChip<FilterKey>[]>(() => {
  const applied = filters.applied.value
  const result: UiFilterChip<FilterKey>[] = []
  if (applied.status !== '') {
    const label = statusOptions.value.find(option => option.value === applied.status)?.label ?? applied.status
    result.push({ key: FilterKey.Status, label: t('shared.status'), value: label })
  }
  if (applied.from !== '') {
    result.push({ key: FilterKey.From, label: t('billing.invoicesList.dateFrom'), value: formatDate(applied.from) })
  }
  if (applied.to !== '') {
    result.push({ key: FilterKey.To, label: t('billing.invoicesList.dateTo'), value: formatDate(applied.to) })
  }
  return result
})

/** The page's invoices, narrowed by the search box. */
const shownInvoices = computed<Invoice[]>(() => searchInvoices(store.invoices, filters.search.value))

/** The paid, due and overdue rollup of the loaded page. */
const summary = computed(() => summarizeInvoices(store.invoices))

/** The selected invoices of the shown rows. */
const selection = useRowSelection(() => shownInvoices.value.map((invoice) => invoice.id))

/** First row number on the page, for the record count. */
const firstShown = computed<number>(() => pageFirstRow(filters.page.value, LEDGER_PAGE_SIZE))

/** Last row number on the page, for the record count. */
const lastShown = computed<number>(() => pageLastRow(filters.page.value, LEDGER_PAGE_SIZE, store.totalCount))

/**
 * What the list is loaded with, as one string, so the list is read again only when a filter or the page changes
 * and not when the search text moves in the address.
 */
const loadKey = computed<string>(() => {
  const applied = filters.applied.value
  return [applied.status, applied.from, applied.to, filters.page.value].join('|')
})

/**
 * Loads the page named by the address, with the applied filters; the selection of the previous page is dropped.
 *
 * @returns Resolves when the request has settled.
 */
const load = async (): Promise<void> => {
  const applied = filters.applied.value
  selection.clear()
  await store.fetchAll(filters.page.value, LEDGER_PAGE_SIZE, applied.status, applied.from, applied.to)
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
  feedback.clear()
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
    testid: `billing-invoice-view-${id}`,
    to: invoiceViewPath(id),
  },
  {
    label: t('shared.edit'),
    testid: `billing-invoice-edit-${id}`,
    to: invoiceEditPath(id),
  },
  {
    label: t('shared.delete'),
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

// The address is the source of truth: the first load and every change of a filter or of the page read the list.
watch(loadKey, load, { immediate: true })
</script>

<template>
  <UiStack data-testid="billing-invoices-view">
    <UiPageHeader :title="t('shared.invoices')" :subtitle="t('billing.invoicesList.manageBillingInvoices')">
      <template #actions>
        <PageHelpLink :url="guideUrl" />
      </template>
    </UiPageHeader>

    <UiGrid :columns="3">
      <UiStatTile
        :label="t('clients.clientSummary.paid')"
        :value="bucketAmountText(summary.paid)"
        :hint="t('billing.invoicesList.summaryHint', { count: summary.paid.count }, summary.paid.count)"
        tone="success"
        data-testid="billing-invoices-summary-paid"
      />
      <UiStatTile
        :label="t('billing.invoicesList.due')"
        :value="bucketAmountText(summary.due)"
        :hint="t('billing.invoicesList.summaryHint', { count: summary.due.count }, summary.due.count)"
        tone="warning"
        data-testid="billing-invoices-summary-due"
      />
      <UiStatTile
        :label="t('clients.clientSummary.overdue')"
        :value="bucketAmountText(summary.overdue)"
        :hint="t('billing.invoicesList.summaryHint', { count: summary.overdue.count }, summary.overdue.count)"
        tone="danger"
        data-testid="billing-invoices-summary-overdue"
      />
    </UiGrid>

    <UiListToolbar
      v-model:search="filters.search.value"
      search-testid="billing-invoices-search"
      :active-count="filters.activeCount.value"
      :chips="chips"
      @apply="filters.apply"
      @reset="filters.reset"
      @cancel="filters.cancel"
      @remove="filters.remove"
      @clear="filters.clearAll"
    >
      <UiStack gap="md">
        <UiField :label="t('shared.status')">
          <UiSelect
            v-model="filters.draft.value.status"
            :options="statusOptions"
            :placeholder="t('shared.status')"
            data-testid="billing-invoices-filter-status"
          />
        </UiField>
        <UiField :label="t('shared.invoiceDate')">
          <UiDateRangePicker v-model="draftRange" :placeholder="t('shared.selectDateRange')" />
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-invoices-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-invoices-error"
      @retry="load"
    />

    <UiText v-if="store.invoices.length > 0" tone="secondary" size="md" data-testid="billing-invoices-count">
      {{ t('billing.invoicesList.totalcountRecordsFoundShowingValue', { totalCount: store.totalCount, value: firstShown, value2: lastShown }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && store.invoices.length === 0"
        :empty="!store.loading && shownInvoices.length === 0"
        :scrollable="false"
        :selection="selection"
        select-all-testid="billing-invoices-select-all"
        data-testid="billing-invoices-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.invoice') }}</UiTableHeader>
            <UiTableHeader hide-below="sm">{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader hide-below="md">{{ t('shared.invoiceDate') }}</UiTableHeader>
            <UiTableHeader hide-below="xl">{{ t('shared.dueDate') }}</UiTableHeader>
            <UiTableHeader numeric hide-below="sm">{{ t('shared.total') }}</UiTableHeader>
            <UiTableHeader hide-below="xl">{{ t('shared.paymentMethod') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="invoice in shownInvoices"
            :key="invoice.id"
            hoverable
            :row-id="invoice.id"
            :select-testid="`billing-invoice-select-${invoice.id}`"
          >
            <UiTableCell muted>
              #{{ invoice.id }}
              <UiText :size="UiTextSize.Xs" class="max-w-32 truncate sm:hidden">{{ invoice.clientName }}</UiText>
            </UiTableCell>
            <UiTableCell hide-below="sm" truncate>
              <UiText class="truncate">{{ invoice.clientName }}</UiText>
            </UiTableCell>
            <UiTableCell muted hide-below="md">{{ formatDate(invoice.createdAt) }}</UiTableCell>
            <UiTableCell muted hide-below="xl">{{ formatDate(invoice.dueDate) }}</UiTableCell>
            <UiTableCell numeric hide-below="sm">{{ formatPlainAmount(invoice.total, invoice.currency) }}</UiTableCell>
            <UiTableCell muted hide-below="xl">{{ invoice.paymentMethod || EMPTY_VALUE }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="invoiceStatusTone(invoice.status)">{{ invoice.status }}</UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="invoiceActions(invoice.id)" :testid="`billing-invoice-actions-${invoice.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState :title="t('shared.noInvoicesFound')" data-testid="billing-invoices-empty" />
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="filters.page.value"
      :total="store.totalCount"
      :page-size="LEDGER_PAGE_SIZE"
      testid="billing-invoices-pagination"
      @update:page="filters.setPage"
    />

    <UiDialog
      variant="confirm"
      :open="pendingDeleteId !== null"
      :title="t('billing.invoicesList.deleteInvoice')"
      :message="t('billing.invoicesList.deleteInvoiceMessage', { id: pendingDeleteId ?? 0 })"
      :confirm-label="t('shared.delete')"
      :loading="processing"
      tone="danger"
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
