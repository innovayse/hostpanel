<script setup lang="ts">
/**
 * Billing invoices list: every client's invoices, filtered by status and invoice date, with
 * paging, bulk actions on the ticked rows and a confirmed delete. Deleting and cancelling are
 * confirmed first; every action reports its outcome (success, or the API's sentence) above the
 * table, and a refused load shows the sentence with a Retry.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useResultBanner } from '@/composables/useResultBanner'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { invoiceStatusTone } from '@/lib/tones'
import { formatPlainAmount } from '@/lib/money'
import { useBillingStore } from '@/stores/billing'
import { pageFirstRow, pageLastRow } from '@/lib/paging'
import { InvoiceBulkAction, InvoiceStatus } from '@/types/invoice'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { invoiceEditPath, invoiceViewPath } from '@/lib/routes'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const store = useBillingStore()

/** Current page (1-based). */
const page = ref(1)

/** True while the filter card is open. */
const filtersOpen = ref(false)

/** Status filter being edited; empty means every status. */
const filterStatus = ref('')

/** Invoice-date range being edited. */
const filterDateRange = ref<[string, string] | null>(null)

/** Status filter the list was last loaded with. */
const appliedStatus = ref('')

/** Date range the list was last loaded with. */
const appliedRange = ref<[string, string] | null>(null)

/** Invoice waiting for the delete confirmation, null when none. */
const pendingDeleteId = ref<number | null>(null)

/** The destructive bulk action (delete or cancel) waiting for confirmation, null when none. */
const pendingBulk = ref<InvoiceBulkAction | null>(null)

/** True while a bulk action or a delete is in flight. */
const processing = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last action. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** True when the pending bulk action is the delete (otherwise it is the cancel). */
const pendingBulkIsDelete = computed<boolean>(() => pendingBulk.value === InvoiceBulkAction.Delete)

/** Options of the status filter: the shared lifecycle statuses plus the two collection states. */
const statusOptions = computed(() => [
  ...INVOICE_STATUS_OPTIONS,
  { value: InvoiceStatus.Collections, label: t('billing.invoicesList.collections') },
  { value: InvoiceStatus.PaymentPending, label: t('billing.invoicesList.paymentPending') },
])

/** True when every invoice on the page is ticked (and there is at least one). */
const allSelected = computed<boolean>(
  () => store.invoices.length > 0 && store.selectedIds.length === store.invoices.length,
)

/** First row number on the page, for the record count. */
const firstShown = computed<number>(() => pageFirstRow(page.value, LEDGER_PAGE_SIZE))

/** Last row number on the page, for the record count. */
const lastShown = computed<number>(() => pageLastRow(page.value, LEDGER_PAGE_SIZE, store.totalCount))

/**
 * Loads a page with the applied filters.
 *
 * @param next - Page number (1-based).
 */
const load = async (next: number): Promise<void> => {
  page.value = next
  store.selectedIds = []
  await store.fetchAll(
    next,
    LEDGER_PAGE_SIZE,
    appliedStatus.value,
    appliedRange.value?.[0] ?? '',
    appliedRange.value?.[1] ?? '',
  )
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
    await load(page.value)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/**
 * Applies the filter card and loads the first page.
 *
 * @returns Resolves when the request has settled.
 */
const applyFilters = (): Promise<void> => {
  appliedStatus.value = filterStatus.value
  appliedRange.value = filterDateRange.value
  return load(1)
}

/**
 * Clears the filter card and loads the first page.
 *
 * @returns Resolves when the request has settled.
 */
const clearFilters = (): Promise<void> => {
  filterStatus.value = ''
  filterDateRange.value = null
  return applyFilters()
}

/**
 * Ticks or unticks one invoice.
 *
 * @param id - The invoice id.
 */
const toggleSelected = (id: number): void => {
  store.selectedIds = store.selectedIds.includes(id)
    ? store.selectedIds.filter((selected) => selected !== id)
    : [...store.selectedIds, id]
}

/** Ticks every invoice on the page, or clears the ticks when all were ticked. */
const toggleAll = (): void => {
  store.selectedIds = allSelected.value ? [] : store.invoices.map((invoice) => invoice.id)
}

/**
 * Applies a bulk action to the ticked invoices, shows the outcome and reloads the page.
 *
 * @param action - What to do with them.
 * @returns Resolves when the request (and the reload) has settled.
 */
const runBulk = (action: InvoiceBulkAction): Promise<void> =>
  runCommand(() => store.bulkAction(store.selectedIds, action), 'billing.invoicesList.bulkActionDone')

/**
 * Runs the confirmed destructive bulk action and closes the prompt.
 *
 * @returns Resolves when the request (and the reload) has settled.
 */
const confirmBulk = async (): Promise<void> => {
  const action = pendingBulk.value
  if (action === null) {
    return
  }
  await runBulk(action)
  pendingBulk.value = null
}

/** Closes the bulk confirmation; ignored while the action runs. */
const dismissBulk = (): void => {
  if (!processing.value) {
    pendingBulk.value = null
  }
}

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

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="billing-invoices-view">
    <UiPageHeader :title="t('shared.invoices')" :subtitle="t('billing.invoicesList.manageBillingInvoices')">
      <template #actions>
        <UiButton variant="secondary" data-testid="billing-invoices-filter-toggle" @click="filtersOpen = !filtersOpen">
          {{ filtersOpen ? t('shared.hideFilters') : t('shared.searchFilter') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiFilterCard v-if="filtersOpen">
      <UiGrid :columns="2" container class="items-end">
        <UiField :label="t('shared.status')">
          <UiSelect
            v-model="filterStatus"
            :options="statusOptions"
            :placeholder="t('shared.status')"
            data-testid="billing-invoices-filter-status"
          />
        </UiField>
        <UiField :label="t('shared.invoiceDate')">
          <UiDateRangePicker v-model="filterDateRange" :placeholder="t('shared.selectDateRange')" />
        </UiField>
      </UiGrid>
      <template #actions>
        <UiStack direction="row" justify="center" gap="sm">
          <UiButton variant="primary" data-testid="billing-invoices-search" @click="applyFilters">
            <UiIcon :markup="Icon.Search" size="sm" />
            {{ t('shared.search2') }}
          </UiButton>
          <UiButton variant="ghost" data-testid="billing-invoices-clear" @click="clearFilters">
            {{ t('shared.clear') }}
          </UiButton>
        </UiStack>
      </template>
    </UiFilterCard>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-invoices-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-invoices-error"
      @retry="load(page)"
    />

    <UiText v-if="store.invoices.length > 0" tone="secondary" size="md" data-testid="billing-invoices-count">
      {{ t('billing.invoicesList.totalcountRecordsFoundShowingValue', { totalCount: store.totalCount, value: firstShown, value2: lastShown }) }}
    </UiText>

    <UiCard v-if="store.selectedIds.length > 0" padding="md" data-testid="billing-invoices-bulk">
      <UiStack direction="row" gap="sm" align="center" wrap>
        <UiText tone="primary" size="md">{{ t('shared.withSelected') }}</UiText>
        <UiButton variant="secondary" size="sm" data-testid="billing-invoices-bulk-paid" @click="runBulk(InvoiceBulkAction.MarkPaid)">
          {{ t('billing.invoicesList.markPaid') }}
        </UiButton>
        <UiButton variant="secondary" size="sm" data-testid="billing-invoices-bulk-unpaid" @click="runBulk(InvoiceBulkAction.MarkUnpaid)">
          {{ t('billing.invoicesList.markUnpaid') }}
        </UiButton>
        <UiButton variant="secondary" size="sm" data-testid="billing-invoices-bulk-cancelled" @click="pendingBulk = InvoiceBulkAction.MarkCancelled">
          {{ t('shared.markCancelled') }}
        </UiButton>
        <UiButton variant="secondary" size="sm" data-testid="billing-invoices-bulk-duplicate" @click="runBulk(InvoiceBulkAction.Duplicate)">
          {{ t('billing.invoicesList.duplicateInvoice') }}
        </UiButton>
        <UiButton variant="danger" size="sm" data-testid="billing-invoices-bulk-delete" @click="pendingBulk = InvoiceBulkAction.Delete">
          {{ t('shared.delete') }}
        </UiButton>
      </UiStack>
    </UiCard>

    <UiCard>
      <UiTable
        :loading="store.loading && store.invoices.length === 0"
        :empty="!store.loading && store.invoices.length === 0"
        :colspan="9"
        data-testid="billing-invoices-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>
              <UiCheckbox
                :model-value="allSelected"
                :aria-label="t('shared.selectAll')"
                data-testid="billing-invoices-select-all"
                @update:model-value="toggleAll"
              />
            </UiTableHeader>
            <UiTableHeader>{{ t('shared.invoice') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.invoiceDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.dueDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.total') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="invoice in store.invoices" :key="invoice.id" hoverable :selected="store.selectedIds.includes(invoice.id)">
            <UiTableCell>
              <UiCheckbox
                :model-value="store.selectedIds.includes(invoice.id)"
                :aria-label="t('shared.selectRow')"
                :data-testid="`billing-invoice-select-${invoice.id}`"
                @update:model-value="toggleSelected(invoice.id)"
              />
            </UiTableCell>
            <UiTableCell muted>#{{ invoice.id }}</UiTableCell>
            <UiTableCell>{{ invoice.clientName }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(invoice.createdAt) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(invoice.dueDate) }}</UiTableCell>
            <UiTableCell>{{ formatPlainAmount(invoice.total, invoice.currency) }}</UiTableCell>
            <UiTableCell muted>{{ invoice.paymentMethod || '—' }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="invoiceStatusTone(invoice.status)">{{ invoice.status }}</UiBadge>
            </UiTableCell>
            <UiTableCell align="right">
              <UiStack direction="row" gap="xs" justify="end">
                <UiLink
                  variant="ghost"
                  size="sm"
                  :to="invoiceViewPath(invoice.id)"
                  :aria-label="t('shared.view')"
                  :data-testid="`billing-invoice-view-${invoice.id}`"
                >
                  <UiIcon :markup="Icon.Eye" size="sm" />
                </UiLink>
                <UiLink
                  variant="ghost"
                  size="sm"
                  :to="invoiceEditPath(invoice.id)"
                  :aria-label="t('shared.edit')"
                  :data-testid="`billing-invoice-edit-${invoice.id}`"
                >
                  <UiIcon :markup="Icon.Edit" size="sm" />
                </UiLink>
                <UiIconButton
                  :label="t('shared.delete')"
                  size="sm"
                  :data-testid="`billing-invoice-delete-${invoice.id}`"
                  @click="pendingDeleteId = invoice.id"
                >
                  <UiIcon :markup="Icon.TrashBin" size="sm" />
                </UiIconButton>
              </UiStack>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('shared.noInvoicesFound') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="LEDGER_PAGE_SIZE"
      testid="billing-invoices-pagination"
      @update:page="load"
    />

    <UiDialog
      variant="confirm"
      :open="pendingDeleteId !== null"
      :title="t('billing.invoicesList.deleteInvoice')"
      :message="t('billing.invoicesList.deleteInvoiceMessage', { id: pendingDeleteId ?? 0 })"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="processing"
      tone="danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />

    <UiDialog
      variant="confirm"
      :open="pendingBulk !== null"
      :title="pendingBulkIsDelete ? t('billing.invoicesList.deleteInvoices') : t('billing.invoicesList.cancelInvoices')"
      :message="pendingBulkIsDelete
        ? t('billing.invoicesList.deleteInvoicesMessage', { count: store.selectedIds.length }, store.selectedIds.length)
        : t('billing.invoicesList.cancelInvoicesMessage', { count: store.selectedIds.length }, store.selectedIds.length)"
      :confirm-label="pendingBulkIsDelete ? t('shared.delete') : t('shared.markCancelled')"
      :loading="processing"
      tone="danger"
      data-testid="billing-invoices-bulk-confirm"
      @confirm="confirmBulk"
      @close="dismissBulk"
    />
  </UiStack>
</template>
