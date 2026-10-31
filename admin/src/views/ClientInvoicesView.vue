<script setup lang="ts">
/**
 * Client-scoped invoices list: shows invoices for a specific client with filtering, bulk actions,
 * pagination, and create functionality. Deleting and cancelling are confirmed first; every action
 * reports its outcome (success, or the API's sentence) above the list, and a refused load shows
 * the sentence with a Retry.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useBillingStore } from '@/stores/billing'
import { useResultBanner } from '@/composables/useResultBanner'
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'
import { invoiceStatusTone } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { InvoiceBulkAction, InvoiceOpenAction } from '@/types/invoice'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
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
import UiLabel from '@/components/ui/UiLabel.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { invoicePath } from '@/lib/routes'

const { t } = useI18n()

/** Icon markup (shape tags) of the remove-line button. */
const TRASH_ICON = '<polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />'

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open an invoice. */
const router = useRouter()

/** Billing store, the only caller of the invoice endpoints. */
const store = useBillingStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(LEDGER_PAGE_SIZE)

/** Status filter value. */
const filterStatus = ref('')

/** Date from filter. */
const filterFrom = ref('')

/** Date to filter. */
const filterTo = ref('')

/** Whether the select-all checkbox is checked. */
const selectAll = ref(false)

/** Set of selected invoice IDs. */
const selectedIds = ref<Set<number>>(new Set())

/** Bulk action dropdown value. */
const bulkActionValue = ref('')

/** Whether the bulk confirm modal is visible. */
const showBulkConfirmModal = ref(false)

/** Whether the inline create form is visible. */
const showCreateForm = ref(false)

/** New invoice due date. */
const newDueDate = ref('')

/** New invoice items. */
const newItems = ref<Array<{ description: string; unitPrice: number; quantity: number }>>([
  { description: '', unitPrice: 0, quantity: 1 },
])

/** Whether to create as draft. */
const newIsDraft = ref(true)

/** True while creating an invoice. */
const creating = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last action. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

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

/** Whether any items are selected. */
const hasSelection = computed(() => selectedIds.value.size > 0)

/** Bulk action options. */
const bulkActions = [
  { value: '', label: t('clients.clientInvoices.bulkActions') },
  { value: InvoiceBulkAction.MarkPaid, label: t('billing.invoiceSummaryTab.markPaid') },
  { value: InvoiceBulkAction.MarkUnpaid, label: t('billing.invoiceDetail.markUnpaid') },
  { value: InvoiceBulkAction.MarkCancelled, label: t('shared.markCancelled') },
  { value: InvoiceBulkAction.Duplicate, label: t('billing.quoteDetail.duplicate') },
  { value: InvoiceBulkAction.Delete, label: t('shared.delete') },
]

/** True when the chosen bulk action deletes or cancels invoices, so its prompt is in danger style. */
const bulkIsDestructive = computed<boolean>(() =>
  bulkActionValue.value === InvoiceBulkAction.Delete || bulkActionValue.value === InvoiceBulkAction.MarkCancelled,
)

/** Caption of the chosen bulk action. */
const bulkActionLabel = computed<string>(() => bulkActions.find(a => a.value === bulkActionValue.value)?.label ?? '')

/**
 * Fetches client invoices with current filters.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchInvoices = async (): Promise<void> => {
  await store.fetchClientInvoices(
    clientId.value, page.value, pageSize.value,
    filterStatus.value, filterFrom.value, filterTo.value,
  )
  loadError.value = store.error
}

/**
 * Clears the row selection.
 *
 * @returns Nothing.
 */
const clearSelection = (): void => {
  selectedIds.value = new Set()
  selectAll.value = false
}

/**
 * Applies filters and resets to page 1.
 *
 * @returns Nothing.
 */
const applyFilters = (): void => {
  page.value = 1
  clearSelection()
  fetchInvoices()
}

/**
 * Navigates to a specific page.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToPage = (p: number): void => {
  page.value = p
  clearSelection()
  fetchInvoices()
}

/**
 * Toggles selection of all invoices.
 *
 * @returns Nothing.
 */
const toggleSelectAll = (): void => {
  if (selectAll.value) {
    clearSelection()
  } else {
    selectedIds.value = new Set(store.clientInvoices.map(i => i.id))
    selectAll.value = true
  }
}

/**
 * Toggles selection of a single invoice.
 *
 * @param id - The invoice ID.
 * @returns Nothing.
 */
const toggleSelect = (id: number): void => {
  const next = new Set(selectedIds.value)
  if (next.has(id)) {
    next.delete(id)
  } else {
    next.add(id)
  }
  selectedIds.value = next
  selectAll.value = next.size === store.clientInvoices.length && store.clientInvoices.length > 0
}

/**
 * Executes the selected bulk action, shows its outcome and reloads the list on success.
 *
 * @returns Promise that resolves when the action and the reload finish.
 */
const executeBulkAction = async (): Promise<void> => {
  if (!bulkActionValue.value || selectedIds.value.size === 0) {
    showBulkConfirmModal.value = false
    return
  }
  feedback.clear()
  processing.value = true
  const ok = await store.bulkAction(Array.from(selectedIds.value), bulkActionValue.value)
  processing.value = false
  showBulkConfirmModal.value = false
  report(ok, 'clients.clientInvoices.bulkActionDone')
  if (ok) {
    clearSelection()
    bulkActionValue.value = ''
    await fetchInvoices()
  }
}

/** Closes the bulk confirmation; ignored while the action runs. */
const dismissBulk = (): void => {
  if (!processing.value) {
    showBulkConfirmModal.value = false
  }
}

/**
 * Opens the bulk confirm modal.
 *
 * @returns Nothing.
 */
const handleBulkApply = (): void => {
  if (!bulkActionValue.value || selectedIds.value.size === 0) {
    return
  }
  showBulkConfirmModal.value = true
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
    newItems.value = [{ description: '', unitPrice: 0, quantity: 1 }]
    newIsDraft.value = true
  }
}

/**
 * Adds a blank line item.
 *
 * @returns Nothing.
 */
const addNewItem = (): void => {
  newItems.value.push({ description: '', unitPrice: 0, quantity: 1 })
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
  feedback.clear()
  creating.value = true
  const id = await store.createInvoice(Number(clientId.value), newDueDate.value, newItems.value, newIsDraft.value)
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
  feedback.clear()
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

/** Loads the first page. */
onMounted(() => fetchInvoices())
</script>

<template>
  <UiStack>
    <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
      <UiHeading :level="1" variant="card">{{ t('shared.invoices') }}</UiHeading>
      <UiButton
        type="button"
        :variant="UiButtonVariant.Primary"
        data-testid="invoice-create-toggle"
        @click="toggleCreateForm"
      >
        {{ t('clients.clientInvoices.createInvoice') }}
      </UiButton>
    </UiStack>

    <UiCard v-if="showCreateForm" padding="md" data-testid="invoice-create-form">
      <UiStack>
        <UiHeading :level="2" variant="card">{{ t('clients.clientInvoices.newInvoice') }}</UiHeading>

        <UiGrid :columns="2">
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
          <UiStack gap="sm">
            <UiStack
              v-for="(item, idx) in newItems"
              :key="idx"
              direction="row"
              gap="sm"
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
                :label="t('shared.delete')"
                :size="UiSize.Sm"
                :data-testid="`invoice-item-remove-${idx}`"
                @click="removeNewItem(idx)"
              >
                <UiIcon :markup="TRASH_ICON" />
              </UiIconButton>
            </UiStack>
            <UiStack direction="row">
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

        <UiStack direction="row" :justify="UiJustify.End" gap="sm">
          <UiButton
            type="button"
            :variant="UiButtonVariant.Secondary"
            data-testid="invoice-create-cancel"
            @click="showCreateForm = false"
          >
            {{ t('shared.cancel') }}
          </UiButton>
          <UiButton
            type="button"
            :variant="UiButtonVariant.Primary"
            :disabled="creating"
            data-testid="invoice-create-submit"
            @click="handleCreate"
          >
            {{ creating ? t('shared.creating') : t('clients.clientInvoices.createInvoice2') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiCard>

    <UiStack direction="row" wrap :align="UiAlignItems.Center" gap="sm">
      <UiSelect v-model="filterStatus" :options="INVOICE_STATUS_OPTIONS" :placeholder="t('clients.clientInvoices.allStatuses')" data-testid="client-invoices-filter-status" />
      <UiDatePicker v-model="filterFrom" :placeholder="t('clients.clientInvoices.from')" />
      <UiDatePicker v-model="filterTo" :placeholder="t('shared.to')" />
      <UiButton
        type="button"
        :variant="UiButtonVariant.Secondary"
        data-testid="client-invoices-filter"
        @click="applyFilters"
      >
        {{ t('clients.clientInvoices.filter') }}
      </UiButton>
    </UiStack>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="client-invoices-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="client-invoices-error"
      @retry="fetchInvoices()"
    />

    <UiStack v-if="store.loading && store.clientInvoices.length === 0" data-testid="client-invoices-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientInvoices.loadingInvoices') }}</UiText>
    </UiStack>

    <template v-else>
      <UiCard v-if="hasSelection" padding="sm" data-testid="client-invoices-bulk">
        <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
            {{ t('clients.clientInvoices.sizeSelected', { size: selectedIds.size }) }}
          </UiText>
          <UiSelect v-model="bulkActionValue" :options="bulkActions" :placeholder="t('clients.clientInvoices.bulkActions')" data-testid="client-invoices-bulk-action" />
          <UiButton
            type="button"
            :variant="UiButtonVariant.Secondary"
            :size="UiSize.Sm"
            :disabled="!bulkActionValue"
            data-testid="client-invoices-bulk-apply"
            @click="handleBulkApply"
          >
            {{ t('shared.apply') }}
          </UiButton>
        </UiStack>
      </UiCard>

      <UiCard class="overflow-x-auto">
        <UiTable
          density="comfortable"
          :empty="store.clientInvoices.length === 0"
          :colspan="9"
          data-testid="client-invoices-table"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>
                <UiCheckbox
                  :model-value="selectAll"
                  :aria-label="t('shared.selectAll')"
                  data-testid="client-invoices-select-all"
                  @update:model-value="toggleSelectAll"
                />
              </UiTableHeader>
              <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.invoiceDate') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.dueDate') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.datePaid') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.total') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="invoice in store.clientInvoices" :key="invoice.id" hoverable>
              <UiTableCell>
                <UiCheckbox
                  :model-value="selectedIds.has(invoice.id)"
                  :aria-label="`#${invoice.id}`"
                  :data-testid="`client-invoices-select-${invoice.id}`"
                  @update:model-value="toggleSelect(invoice.id)"
                />
              </UiTableCell>
              <UiTableCell muted>#{{ invoice.id }}</UiTableCell>
              <UiTableCell muted>{{ formatDate(invoice.invoiceDate) }}</UiTableCell>
              <UiTableCell muted>{{ formatDate(invoice.dueDate) }}</UiTableCell>
              <UiTableCell muted>{{ invoice.paidAt ? formatDate(invoice.paidAt) : '-' }}</UiTableCell>
              <UiTableCell>{{ formatPlainAmount(invoice.total, invoice.currency) }}</UiTableCell>
              <UiTableCell muted>{{ invoice.paymentMethod || '-' }}</UiTableCell>
              <UiTableCell>
                <UiBadge :tone="invoiceStatusTone(invoice.status)" small>{{ invoice.status }}</UiBadge>
              </UiTableCell>
              <UiTableCell>
                <UiStack direction="row" gap="sm" :align="UiAlignItems.Center">
                  <UiButton
                    type="button"
                    :variant="UiButtonVariant.Ghost"
                    :size="UiSize.Sm"
                    :data-testid="`client-invoices-view-${invoice.id}`"
                    @click="openInvoice(invoice.id, InvoiceOpenAction.View)"
                  >
                    {{ t('shared.view') }}
                  </UiButton>
                  <UiButton
                    type="button"
                    :variant="UiButtonVariant.Secondary"
                    :size="UiSize.Sm"
                    :data-testid="`client-invoices-edit-${invoice.id}`"
                    @click="openInvoice(invoice.id, InvoiceOpenAction.Edit)"
                  >
                    {{ t('shared.edit') }}
                  </UiButton>
                  <UiButton
                    type="button"
                    :variant="UiButtonVariant.Danger"
                    :size="UiSize.Sm"
                    :data-testid="`client-invoices-delete-${invoice.id}`"
                    @click="confirmDelete(invoice.id)"
                  >
                    {{ t('shared.delete') }}
                  </UiButton>
                </UiStack>
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            <UiEmptyState :title="t('shared.noInvoicesFound')" />
          </template>
        </UiTable>
        <UiStack
          v-if="store.clientInvoicesTotal > pageSize"
          direction="row"
          :justify="UiJustify.Between"
          :align="UiAlignItems.Center"
        >
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
            {{ t('clients.clientInvoices.clientinvoicestotalInvoiceValue', { clientInvoicesTotal: store.clientInvoicesTotal }) }}
          </UiText>
          <UiPagination
            :page="page"
            :total="store.clientInvoicesTotal"
            :page-size="pageSize"
            testid="client-invoices-pagination"
            @update:page="goToPage"
          />
        </UiStack>
      </UiCard>
    </template>

    <UiDialog
      v-if="showDeleteModal"
      variant="confirm"
      :title="t('clients.clientInvoices.deleteInvoice')"
      :message="t('clients.clientInvoices.confirmDeleteInvoice', { id: deleteTargetId })"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="processing"
      tone="danger"
      @confirm="executeDelete"
      @close="dismissDelete"
    />

    <UiDialog
      v-if="showBulkConfirmModal"
      variant="confirm"
      :title="t('clients.clientInvoices.confirmBulkAction')"
      :message="t('clients.clientInvoices.confirmBulkApply', { action: bulkActionLabel, count: selectedIds.size }, selectedIds.size)"
      :confirm-label="bulkActionLabel"
      :loading-label="t('shared.processing')"
      :loading="processing"
      :tone="bulkIsDestructive ? UiTone.Danger : UiTone.Primary"
      @confirm="executeBulkAction"
      @close="dismissBulk"
    />
  </UiStack>
</template>
