<script setup lang="ts">
/**
 * Client billable items page: displays uninvoiced and invoiced billable items and allows adding
 * new items, time billing entries, and invoicing or deleting selected items. Invoicing and deleting
 * are confirmed first; every action reports its outcome (success, or the API's sentence) above the
 * lists, and a refused load shows the sentence with a Retry.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientBillableItemsStore } from '@/stores/clientBillableItems'
import { useResultBanner } from '@/composables/useResultBanner'
import { formatPlainAmount } from '@/lib/money'
import AddBillableItemModal from '@/components/clients/AddBillableItemModal.vue'
import AddTimeBillingModal from '@/components/clients/AddTimeBillingModal.vue'
import type { ClientBillableItem, CurrencyAmount } from '@/types/billableitem'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
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
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

const { t } = useI18n()

/** Icon markup (shape tags) of the delete button. */
const TRASH_ICON = '<polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />'

/** Current route, source of the client id. */
const route = useRoute()

/** Client billable items store, the only caller of the billable item endpoints. */
const store = useClientBillableItemsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** True while the initial load is in flight (owned by the store). */
const loading = computed(() => store.loading)

/** The sentence of the last refused load, shown with Retry; null when the items loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last action. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

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

/** Uninvoiced billable items for this client. */
const uninvoicedItems = ref<ClientBillableItem[]>([])

/** Invoiced billable items for this client (current page). */
const invoicedItems = ref<ClientBillableItem[]>([])

/** Sums of the uninvoiced item amounts, one per currency. */
const uninvoicedTotal = ref<CurrencyAmount[]>([])

/**
 * The uninvoiced totals, one entry per currency the items are in, written with
 * `formatPlainAmount` and joined with commas. Amounts of different currencies are never added; with
 * no items the total reads as zero.
 */
const uninvoicedTotalLabel = computed(() =>
  uninvoicedTotal.value.length === 0
    ? formatPlainAmount(0)
    : uninvoicedTotal.value.map((entry) => formatPlainAmount(entry.amount, entry.currency)).join(', '))

/** Current invoiced items page (1-based). */
const invoicedPage = ref(1)

/** Invoiced items per page. */
const invoicedPageSize = ref(DEFAULT_PAGE_SIZE)

/** Total number of invoiced items across all pages. */
const invoicedTotalCount = ref(0)

/** Whether the add billable item modal is visible. */
const showAddItemModal = ref(false)

/** Whether the time billing modal is visible. */
const showTimeBillingModal = ref(false)

/** Set of selected uninvoiced item IDs. */
const selectedIds = ref<Set<number>>(new Set())

/** Whether all uninvoiced items are selected. */
const selectAll = ref(false)

/** Whether the invoice confirmation modal is visible. */
const showInvoiceConfirmModal = ref(false)

/** Whether the delete confirmation modal is visible. */
const showDeleteConfirmModal = ref(false)

/** ID of the item pending single deletion, or null for bulk delete. */
const pendingDeleteId = ref<number | null>(null)

/** True while invoicing is in progress. */
const invoicing = ref(false)

/** True while a delete is in progress. */
const deleting = ref(false)

/**
 * Fetches billable items for this client from the backend.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchBillableItems = async (): Promise<void> => {
  const result = await store.fetchItems(clientId.value, invoicedPage.value, invoicedPageSize.value)
  loadError.value = result ? null : store.error
  if (result) {
    uninvoicedItems.value = result.uninvoicedItems
    uninvoicedTotal.value = result.uninvoicedTotal
    invoicedItems.value = result.invoicedItems.items
    invoicedTotalCount.value = result.invoicedItems.totalCount
  }
}

/**
 * Invoices all selected uninvoiced items.
 *
 * @returns Promise that resolves when the operation completes.
 */
const handleInvoiceSelected = async (): Promise<void> => {
  feedback.clear()
  invoicing.value = true
  const invoiced = await store.invoiceSelected(clientId.value, Array.from(selectedIds.value))
  invoicing.value = false
  showInvoiceConfirmModal.value = false
  report(invoiced, 'clients.clientBillableItems.itemsInvoiced')
  if (invoiced) {
    selectedIds.value = new Set()
    selectAll.value = false
    await fetchBillableItems()
  }
}

/** Closes the invoice confirmation; ignored while invoicing runs. */
const dismissInvoice = (): void => {
  if (!invoicing.value) {
    showInvoiceConfirmModal.value = false
  }
}

/**
 * Stages a single item for deletion; the confirm dialog does the work.
 *
 * @param id - The billable item ID to delete.
 * @returns Nothing.
 */
const handleDelete = (id: number): void => {
  pendingDeleteId.value = id
  showDeleteConfirmModal.value = true
}

/**
 * Confirms the pending delete action (single or bulk).
 *
 * @returns Promise that resolves when deletion completes.
 */
const confirmDelete = async (): Promise<void> => {
  feedback.clear()
  deleting.value = true
  const ids = pendingDeleteId.value !== null ? [pendingDeleteId.value] : Array.from(selectedIds.value)
  const deleted = await store.deleteItems(clientId.value, ids)
  showDeleteConfirmModal.value = false
  report(deleted, 'clients.clientBillableItems.itemsDeleted')
  if (deleted) {
    if (pendingDeleteId.value === null) {
      selectedIds.value = new Set()
      selectAll.value = false
    }
    await fetchBillableItems()
  }
  deleting.value = false
  pendingDeleteId.value = null
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    showDeleteConfirmModal.value = false
    pendingDeleteId.value = null
  }
}

/**
 * Stages all selected uninvoiced items for deletion.
 *
 * @returns Nothing.
 */
const handleDeleteSelected = (): void => {
  if (selectedIds.value.size === 0) {
    return
  }
  pendingDeleteId.value = null
  showDeleteConfirmModal.value = true
}

/**
 * Toggles selection of all uninvoiced items.
 *
 * @returns Nothing.
 */
const toggleSelectAll = (): void => {
  if (selectAll.value) {
    selectedIds.value = new Set()
    selectAll.value = false
  } else {
    selectedIds.value = new Set(uninvoicedItems.value.map(i => i.id))
    selectAll.value = true
  }
}

/**
 * Toggles selection of a single item.
 *
 * @param id - The item ID to toggle.
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
  selectAll.value = next.size === uninvoicedItems.value.length && uninvoicedItems.value.length > 0
}

/**
 * Navigate to a specific invoiced items page and re-fetch.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToInvoicedPage = (p: number): void => {
  invoicedPage.value = p
  fetchBillableItems()
}

/**
 * Handles successful save from the add billable item modal.
 *
 * @returns Nothing.
 */
const onItemSaved = (): void => {
  showAddItemModal.value = false
  feedback.show(t('clients.clientBillableItems.itemAdded'), false)
  void fetchBillableItems()
}

/**
 * Handles successful save from the time billing modal.
 *
 * @returns Nothing.
 */
const onTimeBillingSaved = (): void => {
  showTimeBillingModal.value = false
  feedback.show(t('clients.clientBillableItems.timeBillingAdded'), false)
  void fetchBillableItems()
}

/** Loads the items. */
onMounted(() => fetchBillableItems())
</script>

<template>
  <UiStack>
    <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
      <UiHeading :level="1" variant="card">{{ t('clients.clientBillableItems.billableItems') }}</UiHeading>
      <UiStack direction="row" gap="sm" :align="UiAlignItems.Center">
        <UiButton
          type="button"
          :variant="UiButtonVariant.Secondary"
          data-testid="client-billable-time-billing"
          @click="showTimeBillingModal = true"
        >
          {{ t('clients.clientBillableItems.addTimeBillingEntries') }}
        </UiButton>
        <UiButton
          type="button"
          :variant="UiButtonVariant.Primary"
          data-testid="client-billable-add"
          @click="showAddItemModal = true"
        >
          {{ t('clients.clientBillableItems.addBillableItem') }}
        </UiButton>
      </UiStack>
    </UiStack>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="client-billable-feedback" />

    <UiStack v-if="loading && uninvoicedItems.length === 0 && invoicedItems.length === 0" data-testid="client-billable-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientBillableItems.loadingBillableItems') }}</UiText>
    </UiStack>

    <UiErrorState
      v-else-if="loadError !== null"
      :message="loadError"
      :loading="loading"
      testid="client-billable-error"
      @retry="fetchBillableItems()"
    />

    <template v-else>
      <UiStack>
        <UiHeading :level="2" variant="card">
          {{ t('clients.clientBillableItems.uninvoicedItemsAmountLength', { amount: uninvoicedTotalLabel, length: uninvoicedItems.length }) }}
        </UiHeading>

        <UiCard v-if="uninvoicedItems.length > 0">
          <UiTable density="comfortable" data-testid="client-billable-uninvoiced-table">
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>
                  <UiCheckbox
                    :model-value="selectAll"
                    :aria-label="t('shared.selectAll')"
                    data-testid="client-billable-select-all"
                    @update:model-value="toggleSelectAll"
                  />
                </UiTableHeader>
                <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.amount') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow v-for="item in uninvoicedItems" :key="item.id" hoverable>
                <UiTableCell>
                  <UiCheckbox
                    :model-value="selectedIds.has(item.id)"
                    :aria-label="item.description"
                    :data-testid="`client-billable-select-${item.id}`"
                    @update:model-value="toggleSelect(item.id)"
                  />
                </UiTableCell>
                <UiTableCell muted>{{ item.id }}</UiTableCell>
                <UiTableCell>{{ item.description }}</UiTableCell>
                <UiTableCell muted>{{ formatPlainAmount(item.amount, item.currency) }}</UiTableCell>
                <UiTableCell>
                  <UiIconButton
                    :label="t('shared.delete')"
                    :size="UiSize.Sm"
                    :disabled="deleting"
                    :data-testid="`client-billable-delete-${item.id}`"
                    @click="handleDelete(item.id)"
                  >
                    <UiIcon :markup="TRASH_ICON" />
                  </UiIconButton>
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
          <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
            <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('shared.withSelected') }}</UiText>
            <UiButton
              type="button"
              :variant="UiButtonVariant.Secondary"
              :size="UiSize.Sm"
              :disabled="selectedIds.size === 0 || invoicing"
              data-testid="client-billable-invoice-selected"
              @click="showInvoiceConfirmModal = true"
            >
              {{ t('clients.clientBillableItems.invoiceSelectedItems') }}
            </UiButton>
            <UiButton
              type="button"
              :variant="UiButtonVariant.Danger"
              :size="UiSize.Sm"
              :disabled="selectedIds.size === 0 || deleting"
              data-testid="client-billable-delete-selected"
              @click="handleDeleteSelected"
            >
              {{ t('shared.delete') }}
            </UiButton>
          </UiStack>
        </UiCard>

        <UiCard v-else>
          <UiEmptyState :title="t('clients.clientBillableItems.noUninvoicedBillableItems')" />
        </UiCard>
      </UiStack>

      <UiStack>
        <UiHeading :level="2" variant="card">{{ t('clients.clientBillableItems.invoicedItems') }}</UiHeading>

        <UiCard v-if="invoicedItems.length > 0">
          <UiTable density="comfortable" data-testid="client-billable-invoiced-table">
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.amount') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.invoice') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow v-for="item in invoicedItems" :key="item.id" hoverable>
                <UiTableCell muted>{{ item.id }}</UiTableCell>
                <UiTableCell>{{ item.description }}</UiTableCell>
                <UiTableCell muted>{{ formatPlainAmount(item.amount, item.currency) }}</UiTableCell>
                <UiTableCell>
                  <template v-if="item.invoiceId">#{{ item.invoiceId }}</template>
                  <template v-else>&mdash;</template>
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
          <UiStack
            v-if="invoicedTotalCount > invoicedPageSize"
            direction="row"
            :justify="UiJustify.Between"
            :align="UiAlignItems.Center"
          >
            <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
              {{ t('clients.clientBillableItems.invoicedtotalcountItemValue', { invoicedTotalCount }) }}
            </UiText>
            <UiPagination
              :page="invoicedPage"
              :total="invoicedTotalCount"
              :page-size="invoicedPageSize"
              testid="client-billable-pagination"
              @update:page="goToInvoicedPage"
            />
          </UiStack>
        </UiCard>

        <UiCard v-else>
          <UiEmptyState :title="t('clients.clientBillableItems.noInvoicedBillableItems')" />
        </UiCard>
      </UiStack>
    </template>

    <UiDialog
      v-if="showInvoiceConfirmModal"
      variant="confirm"
      :title="t('clients.clientBillableItems.confirmInvoice')"
      :message="t('clients.clientBillableItems.areYouSureYouWant')"
      :confirm-label="t('shared.ok')"
      :loading-label="t('shared.processing')"
      :loading="invoicing"
      @confirm="handleInvoiceSelected"
      @close="dismissInvoice"
    />

    <UiDialog
      v-if="showDeleteConfirmModal"
      variant="confirm"
      :title="t('clients.clientBillableItems.confirmDelete')"
      :message="pendingDeleteId !== null
        ? t('clients.clientBillableItems.areYouSureYouWant2')
        : t('clients.clientBillableItems.confirmDeleteSelected', { count: selectedIds.size }, selectedIds.size)"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deleting"
      tone="danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />

    <AddBillableItemModal
      v-if="showAddItemModal"
      :client-id="clientId"
      @saved="onItemSaved"
      @close="showAddItemModal = false"
    />

    <AddTimeBillingModal
      v-if="showTimeBillingModal"
      :client-id="clientId"
      :saving="false"
      @saved="onTimeBillingSaved"
      @close="showTimeBillingModal = false"
    />
  </UiStack>
</template>
