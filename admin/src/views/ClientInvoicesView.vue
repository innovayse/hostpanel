<script setup lang="ts">
/**
 * Client-scoped invoices list: shows invoices for a specific client with a search box (invoice number or payment
 * method), a filter dialog (status and invoice date) whose applied filters show as chips, bulk actions on the selected
 * rows (`useRowSelection`, the floating `InvoiceBulkBar`), pagination, and create functionality. The search text, the
 * filters and the page live in the address (`useListQuery`), so a reload or a shared link shows the same list, and the
 * API applies them before it pages. Deleting and cancelling are confirmed first; every action reports its outcome
 * (success, or the API's sentence) above the list, and a refused load shows the sentence with a Retry.
 */
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useBillingStore } from '@/stores/billing'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { InvoiceFilterKey, useInvoiceFilters } from '@/composables/useInvoiceFilters'
import { useListQuery } from '@/composables/useListQuery'
import { useRowSelection } from '@/composables/useRowSelection'
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'
import { invoiceStatusTone, statusLabel } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { InvoiceOpenAction } from '@/types/invoice'
import InvoiceBulkBar from '@/components/billing/InvoiceBulkBar.vue'
import InvoiceFilterFields from '@/components/billing/InvoiceFilterFields.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiCollapse from '@/components/ui/UiCollapse.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ClientInvoicesView.schema'
import type { Invoice, InvoiceBulkAction, InvoiceItemInput } from '@/types/invoice'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant, UiLabelVariant } from '@/utils/ui/text'
import { UiCardPadding } from '@/utils/ui/card'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { invoicePath } from '@/lib/routes'
import { nextRowKey } from '@/lib/rowKey'
import { UiTableDensity } from '@/utils/ui/injection'
import { Icon } from '@/lib/icons'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open an invoice. */
const router = useRouter()

/** Billing store, the only caller of the invoice endpoints. */
const store = useBillingStore()

/** True while the first load is in flight and there are no invoices yet; the table then draws its placeholder rows. */
const initialLoading = computed<boolean>(() => store.loading && store.clientInvoices.length === 0)

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Items per page. */
const pageSize = LEDGER_PAGE_SIZE

/** The filters, the search text and the page of the list, as the address holds them. */
const list = useListQuery(Object.values(InvoiceFilterKey))

/** The applied status and period, and their chips. */
const invoiceFilters = useInvoiceFilters(list)

/** The invoice-date range of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(list.draft, InvoiceFilterKey.From, InvoiceFilterKey.To)

/** One chip per applied filter, in the order they are shown. */
const chips = list.chipsOf(invoiceFilters.chipSources)

/** The selected invoices of the page. */
const selection = useRowSelection(() => store.clientInvoices.map((invoice) => invoice.id))

/** Whether the inline create form is visible. */
const showCreateForm = ref(false)

/** New invoice due date. */
const newDueDate = ref('')

/** A line of the invoice being created: the wire fields plus a stable key for list rendering. */
interface NewInvoiceLine extends InvoiceItemInput {
  /** Stable key of the row; never sent to the API. */
  key: number
}

/**
 * A blank line with a fresh key.
 *
 * @returns The blank line.
 */
const blankInvoiceLine = (): NewInvoiceLine => ({ key: nextRowKey(), description: '', unitPrice: 0, quantity: 1 })

/** New invoice items. */
const newItems = ref<NewInvoiceLine[]>([blankInvoiceLine()])

/** Whether to create as draft. */
const newIsDraft = ref(true)

/** True while creating an invoice. */
const creating = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last action. */
const feedback = useOutcomeToast()

/** True while a bulk action or a delete is in flight. */
const processing = ref(false)

/**
 * Shows the outcome of one action: the success sentence, or the API's sentence of the refusal.
 *
 * @param ok - Whether the API accepted the action.
 * @param successKey - Locale key of the success sentence.
 */
const report = (ok: boolean, successKey: string): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/**
 * Loads the page the address names, with the search text and filters it holds; the selection of the previous page is dropped.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchInvoices = async (): Promise<void> => {
  selection.clear()
  await store.fetchClientInvoices(clientId.value, list.page.value, { ...invoiceFilters.filters.value, search: list.appliedSearch.value })
  loadError.value = store.error
}

/**
 * Applies a bulk action to the selected invoices, shows its outcome and reloads the list on success.
 *
 * @param action - What to do with them.
 * @returns Promise that resolves when the action and the reload finish.
 */
const runBulk = async (action: InvoiceBulkAction): Promise<void> => {
  const ok = await store.bulkAction([...selection.ids.value], action)
  report(ok, 'clients.clientInvoices.bulkActionDone')
  if (ok) {
    selection.clear()
    await fetchInvoices()
  }
}

/**
 * Toggles the inline create form visibility.
 *
 * @returns Nothing.
 */
const toggleCreateForm = (): void => {
  showCreateForm.value = !showCreateForm.value
  if (showCreateForm.value) {
    newDueDate.value = ''
    newItems.value = [blankInvoiceLine()]
    newIsDraft.value = true
  }
}

/**
 * Adds a blank line item.
 *
 * @returns Nothing.
 */
const addNewItem = (): void => {
  newItems.value.push(blankInvoiceLine())
}

/**
 * Removes a line item.
 *
 * @param index - Index to remove.
 * @returns Nothing.
 */
const removeNewItem = (index: number): void => {
  newItems.value.splice(index, 1)
}

/**
 * Creates the invoice and navigates to its detail.
 *
 * @returns Promise that resolves when created.
 */
const handleCreate = async (): Promise<void> => {
  creating.value = true
  const id = await store.createInvoice(Number(clientId.value), newDueDate.value, newItems.value.map(({ description, unitPrice, quantity }) => ({ description, unitPrice, quantity })), newIsDraft.value)
  creating.value = false
  if (id) {
    showCreateForm.value = false
    router.push(invoicePath(id))
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Invoice ID pending deletion. */
const deleteTargetId = ref<number | null>(null)

/** Whether the delete confirm modal is visible. */
const showDeleteModal = ref(false)

/**
 * Opens the delete confirmation modal for an invoice.
 *
 * @param id - The invoice ID to delete.
 * @returns Nothing.
 */
const confirmDelete = (id: number): void => {
  deleteTargetId.value = id
  showDeleteModal.value = true
}

/**
 * Deletes the targeted invoice, shows the outcome and refreshes the list on success.
 *
 * @returns Promise that resolves when deletion completes.
 */
const executeDelete = async (): Promise<void> => {
  if (!deleteTargetId.value) {
    return
  }
  processing.value = true
  const ok = await store.deleteInvoice(deleteTargetId.value)
  processing.value = false
  showDeleteModal.value = false
  deleteTargetId.value = null
  report(ok, 'clients.clientInvoices.invoiceDeleted')
  if (ok) {
    await fetchInvoices()
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!processing.value) {
    showDeleteModal.value = false
    deleteTargetId.value = null
  }
}

/**
 * Opens an invoice in the given mode.
 *
 * @param id - The invoice ID.
 * @param mode - `view` or `edit`.
 * @returns Nothing.
 */
const openInvoice = (id: number, mode: InvoiceOpenAction): void => {
  router.push(`${invoicePath(id)}?mode=${mode}`)
}

/**
 * The row actions of an invoice: view, edit, and delete last (confirmed first).
 *
 * @param invoice - The row.
 * @returns The action list passed to UiRowActions.
 */
const invoiceActions = (invoice: Invoice): UiMenuAction[] => [
  {
    label: t('shared.view'),
    icon: Icon.Eye,
    testid: `client-invoices-view-${invoice.id}`,
    /**
     * Opens this invoice to view it.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      openInvoice(invoice.id, InvoiceOpenAction.View)
    },
  },
  {
    label: t('shared.edit'),
    icon: Icon.Edit,
    testid: `client-invoices-edit-${invoice.id}`,
    /**
     * Opens this invoice to edit it.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      openInvoice(invoice.id, InvoiceOpenAction.Edit)
    },
  },
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    testid: `client-invoices-delete-${invoice.id}`,
    /**
     * Asks for confirmation, then deletes this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      confirmDelete(invoice.id)
    },
    danger: true,
  },
]

// The address is the source of truth: the first load and every change of a filter, the search or the page read the list.
list.loadOnChange(fetchInvoices)
</script>

<template>
  <UiStack>
    <UiSectionHeader :level="1" :variant="UiHeadingVariant.Card">
      {{ t('shared.invoices') }}
      <template #actions>
        <UiButton :action="UiActionKind.Add"
          type="button"
          :variant="UiButtonVariant.Primary"
          data-testid="invoice-create-toggle"
          @click="toggleCreateForm"
        >
          {{ t('clients.clientInvoices.createInvoice') }}
        </UiButton>
      </template>
    </UiSectionHeader>

    <UiCollapse :open="showCreateForm">
      <UiCard :padding="UiCardPadding.Md" container data-testid="invoice-create-form">
        <UiStack>
          <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ t('clients.clientInvoices.newInvoice') }}</UiHeading>

          <UiGrid :columns="2" container>
            <UiField :label="t('shared.dueDate')">
              <UiDatePicker v-model="newDueDate" :direction="UiDatePickerDirection.Future" :placeholder="t('clients.clientInvoices.selectDueDate')" />
            </UiField>
            <UiLabel :variant="UiLabelVariant.Inline">
              <UiCheckbox
                v-model="newIsDraft"
                :aria-label="t('clients.clientInvoices.createAsDraft')"
                data-testid="invoice-create-draft"
              />
              {{ t('clients.clientInvoices.createAsDraft') }}
            </UiLabel>
          </UiGrid>

          <UiField :label="t('shared.lineItems')">
            <UiStack :gap="UiGap.Sm">
              <UiStack
                v-for="(item, idx) in newItems"
                :key="item.key"
                :direction="UiDirection.Row"
                :gap="UiGap.Sm"
                :align="UiAlignItems.Center"
              >
                <UiInput
                  v-model="item.description"
                  type="text"
                  :placeholder="t('shared.description')"
                  data-testid="invoice-item-description"
                />
                <UiNumberInput
                  v-model="item.unitPrice"
                  data-testid="invoice-item-price"
                  :step="0.01"
                  :min="0"
                  :placeholder="t('shared.price')"
                />
                <UiNumberInput
                  v-model="item.quantity"
                  data-testid="invoice-item-quantity"
                  :min="1"
                  :placeholder="t('shared.qty')"
                />
                <UiIconButton
                  v-if="newItems.length > 1"
                  :label="t('shared.remove')"
                  :variant="UiButtonVariant.Ghost"
                  :size="UiSize.Sm"
                  :data-testid="`invoice-item-remove-${idx}`"
                  @click="removeNewItem(idx)"
                >
                  <UiIcon :markup="Icon.Trash" />
                </UiIconButton>
              </UiStack>
              <UiStack :direction="UiDirection.Row">
                <UiButton
                  type="button"
                  :variant="UiButtonVariant.Link"
                  data-testid="invoice-item-add"
                  @click="addNewItem"
                >
                  {{ t('clients.clientInvoices.addItem') }}
                </UiButton>
              </UiStack>
            </UiStack>
          </UiField>

          <UiStack :direction="UiDirection.Row" :justify="UiJustify.End" :gap="UiGap.Sm">
            <UiButton :action="UiActionKind.Cancel"
              type="button"
              :variant="UiButtonVariant.Secondary"
              data-testid="invoice-create-cancel"
              @click="showCreateForm = false"
            >
              {{ t('shared.cancel') }}
            </UiButton>
            <UiButton :action="UiActionKind.Add"
              type="button"
              :variant="UiButtonVariant.Primary"
              :loading="creating"
              data-testid="invoice-create-submit"
              @click="handleCreate"
            >
              {{ t('clients.clientInvoices.createInvoice') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiCard>
    </UiCollapse>

    <UiListToolbar
      v-model:search="list.search.value"
      search-testid="client-invoices-search"
      :search-placeholder="t('billing.invoicesList.searchInvoices')"
      filter-testid="client-invoices-filter"
      :active-count="list.activeCount.value"
      :chips="chips"
      @apply="list.apply"
      @reset="list.reset"
      @cancel="list.cancel"
      @remove="list.remove"
      @clear="list.clearAll"
    >
      <InvoiceFilterFields
        v-model:status="list.draft.value.status"
        v-model:period="draftRange"
        testid-prefix="client-invoices-filter"
      />
    </UiListToolbar>


    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="client-invoices-error"
      @retry="fetchInvoices()"
    />

    <template v-else>
      <UiDataTable
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        :empty="!initialLoading && store.clientInvoices.length === 0"
        :selection="selection"
        select-all-testid="client-invoices-select-all"
        :page="list.page.value"
        :total="store.clientInvoicesTotal"
        :page-size="pageSize"
        pagination-testid="client-invoices-pagination"
        data-testid="client-invoices-table"
        @update:page="list.setPage"
      >
        <UiSchemaTableHead :columns="schema.table.columns" />
        <UiTableBody>
          <UiTableRow
            v-for="invoice in store.clientInvoices"
            :key="invoice.id"
            hoverable
            :row-id="invoice.id"
            :select-testid="`client-invoices-select-${invoice.id}`"
          >
            <UiTableCell muted>#{{ invoice.id }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(invoice.invoiceDate) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(invoice.dueDate) }}</UiTableCell>
            <UiTableCell muted>{{ invoice.paidAt ? formatDate(invoice.paidAt) : '-' }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(invoice.total, invoice.currency) }}</UiTableCell>
            <UiTableCell muted>{{ invoice.paymentMethod || '-' }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="invoiceStatusTone(invoice.status)" small>{{ statusLabel(INVOICE_STATUS_OPTIONS, invoice.status) }}</UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="invoiceActions(invoice)" :testid="`client-invoices-actions-${invoice.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState :title="t('shared.noInvoicesFound')" />
        </template>
      </UiDataTable>
    </template>

    <UiDialog
      v-if="showDeleteModal"
      :variant="UiDialogVariant.Confirm"
      :title="t('clients.clientInvoices.deleteInvoice')"
      :message="t('clients.clientInvoices.confirmDeleteInvoice', { id: deleteTargetId })"
      :confirm-label="t('shared.delete')"
      :loading="processing"
      :tone="UiTone.Danger"
      @confirm="executeDelete"
      @close="dismissDelete"
    />

    <InvoiceBulkBar
      :count="selection.count.value"
      :perform="runBulk"
      testid="client-invoices-bulk"
      @clear="selection.clear"
    />
  </UiStack>
</template>
