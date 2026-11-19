<script setup lang="ts">
/**
 * Client billable items page: displays uninvoiced and invoiced billable items and allows adding
 * new items, time billing entries, and invoicing or deleting the selected items (`useRowSelection`,
 * the floating `UiBulkBar`). Invoicing and deleting
 * are confirmed first; every action reports its outcome (success, or the API's sentence) above the
 * lists, and a refused load shows the sentence with a Retry.
 *
 * The search text (matched against the description and the item id), the type filter and the page of the
 * invoiced items live in the address (`useListQuery`), so a reload or a shared link restores the same lists;
 * the API narrows both lists before it pages the invoiced one.
 */
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientBillableItemsStore } from '@/stores/clientBillableItems'
import { useListQuery } from '@/composables/useListQuery'
import { useResultBanner } from '@/composables/useResultBanner'
import { useRowSelection } from '@/composables/useRowSelection'
import { BILLABLE_ITEM_TYPE_OPTIONS } from '@/lib/constants'
import { formatPlainAmount } from '@/lib/money'
import { statusLabel } from '@/lib/tones'
import AddBillableItemModal from '@/components/clients/AddBillableItemModal.vue'
import AddTimeBillingModal from '@/components/clients/AddTimeBillingModal.vue'
import BillableItemTypeField from '@/components/billing/BillableItemTypeField.vue'
import { BillableItemType } from '@/types/billableitem'
import type { ClientBillableItem, ClientBillableItemFilters, CurrencyAmount } from '@/types/billableitem'
import UiButton from '@/components/ui/UiButton.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiBulkBar from '@/components/ui/UiBulkBar.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import { BILLABLE_ITEMS_CARDS_PART, INVOICED_ITEMS_TABLE, UNINVOICED_ITEMS_TABLE } from '@/views/ClientBillableItemsView.schema'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import type { UiBulkAction } from '@/utils/ui/bulkBar'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { UiTableDensity } from '@/utils/ui/injection'
import { EMPTY_VALUE } from '@/lib/format'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Client billable items store, the only caller of the billable item endpoints. */
const store = useClientBillableItemsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** True while the initial load is in flight (owned by the store). */
const loading = computed(() => store.loading)

/** True while the first load is in flight and there is nothing to show yet; the sections then draw their placeholders. */
const initialLoading = computed(() => loading.value && uninvoicedItems.value.length === 0 && invoicedItems.value.length === 0)

/** The sentence of the last refused load, shown with Retry; null when the items loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last action. */
const feedback = useResultBanner()

/** The filter fields the dialog edits; each is one query key and one `ClientBillableItemFilters` member. */
const FilterKey = {
  Type: 'type',
} as const

/** The search text, the type filter and the invoiced page of the lists, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The type in the address as a billable item type, undefined when none is set or the value is not one. */
const typeOfAddress = enumOf(FilterKey.Type, BillableItemType)

/** What the address narrows both lists by, as the store sends it to the API. */
const filters = computed<ClientBillableItemFilters>(() => ({
  search: appliedSearch.value,
  type: typeOfAddress.value,
}))

/** The applied type as a removable chip, none while the lists are not filtered. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Type,
    label: t('shared.type'),
    value: typeOfAddress.value === undefined ? undefined : statusLabel(BILLABLE_ITEM_TYPE_OPTIONS, typeOfAddress.value),
  },
])

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

/** The selected uninvoiced items. */
const selection = useRowSelection(() => uninvoicedItems.value.map((item) => item.id))

/** Invoiced billable items for this client (current page). */
const invoicedItems = ref<ClientBillableItem[]>([])

/** Sums of the uninvoiced item amounts, one per currency. */
const uninvoicedTotal = ref<CurrencyAmount[]>([])

/**
 * The uninvoiced totals, one entry per currency the items are in, written with
 * `formatPlainAmount` and joined with commas. Amounts of different currencies are never added; with
 * no items the label is empty, because no currency is known and none is made up.
 */
const uninvoicedTotalLabel = computed(() =>
  uninvoicedTotal.value.map((entry) => formatPlainAmount(entry.amount, entry.currency)).join(', '))

/** Invoiced items per page. */
const invoicedPageSize = DEFAULT_PAGE_SIZE

/** Total number of invoiced items across all pages. */
const invoicedTotalCount = ref(0)

/** Whether the add billable item modal is visible. */
const showAddItemModal = ref(false)

/** Whether the time billing modal is visible. */
const showTimeBillingModal = ref(false)

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

/** Keys of the two bulk actions of the bar. */
const BulkKey = { Invoice: 'invoice', Delete: 'delete' } as const

/** The actions of the bulk bar: invoice the selected items (primary) or delete them. */
const bulkActions = computed<UiBulkAction[]>(() => [
  { key: BulkKey.Invoice, label: t('clients.clientBillableItems.invoiceSelected'), title: t('clients.clientBillableItems.invoiceSelectedItems'), icon: Icon.File, variant: UiButtonVariant.Primary, disabled: invoicing.value, testid: 'client-billable-invoice-selected' },
  { key: BulkKey.Delete, label: t('shared.delete'), title: t('shared.delete'), icon: Icon.Trash, variant: UiButtonVariant.Danger, disabled: deleting.value, testid: 'client-billable-delete-selected' },
])

/**
 * Fetches billable items for this client from the backend: the page of invoiced items the address names, with the
 * search and type filter it holds applied to both lists.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchBillableItems = async (): Promise<void> => {
  const result = await store.fetchItems(clientId.value, page.value, invoicedPageSize, filters.value)
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
  const invoiced = await store.invoiceSelected(clientId.value, [...selection.ids.value])
  invoicing.value = false
  showInvoiceConfirmModal.value = false
  report(invoiced, 'clients.clientBillableItems.itemsInvoiced')
  if (invoiced) {
    selection.clear()
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
  const ids = pendingDeleteId.value !== null ? [pendingDeleteId.value] : [...selection.ids.value]
  const deleted = await store.deleteItems(clientId.value, ids)
  showDeleteConfirmModal.value = false
  report(deleted, 'clients.clientBillableItems.itemsDeleted')
  if (deleted) {
    if (pendingDeleteId.value === null) {
      selection.clear()
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
  if (selection.count.value === 0) {
    return
  }
  pendingDeleteId.value = null
  showDeleteConfirmModal.value = true
}

/**
 * Runs the bulk action the operator chose on the bar.
 *
 * @param key - The key of the action, a `BulkKey` value.
 * @returns Nothing.
 */
const runBulkAction = (key: string): void => {
  if (key === BulkKey.Invoice) {
    showInvoiceConfirmModal.value = true
    return
  }
  handleDeleteSelected()
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

/**
 * The row actions of an uninvoiced billable item: one, delete it (confirmed first).
 *
 * @param item - The row.
 * @returns The action list passed to UiRowActions.
 */
const billableItemActions = (item: ClientBillableItem): UiMenuAction[] => [
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    testid: `client-billable-delete-${item.id}`,
    /**
     * Asks for confirmation, then deletes this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      handleDelete(item.id)
    },
    danger: true,
    disabled: deleting.value,
  },
]

// Every change of the search, the type filter or the page loads the lists the address names.
loadOnChange(fetchBillableItems)
</script>

<template>
  <UiStack>
    <UiSectionHeader :level="1" variant="card">
      {{ t('clients.clientBillableItems.billableItems') }}
      <template #actions>
        <UiButton :action="UiActionKind.Add"
          type="button"
          :variant="UiButtonVariant.Secondary"
          data-testid="client-billable-time-billing"
          @click="showTimeBillingModal = true"
        >
          {{ t('clients.clientBillableItems.addTimeBillingEntries') }}
        </UiButton>
        <UiButton :action="UiActionKind.Add"
          type="button"
          :variant="UiButtonVariant.Primary"
          data-testid="client-billable-add"
          @click="showAddItemModal = true"
        >
          {{ t('clients.clientBillableItems.addBillableItem') }}
        </UiButton>
      </template>
    </UiSectionHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="client-billable-search"
      :search-placeholder="t('clients.clientBillableItems.searchItems')"
      filter-testid="client-billable-filter"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <BillableItemTypeField v-model="draft.type" testid="client-billable-filter-type" />
    </UiListToolbar>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="client-billable-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="loading"
      testid="client-billable-error"
      @retry="fetchBillableItems()"
    />

    <UiDetailCards v-else :columns="BILLABLE_ITEMS_CARDS_PART.columns" :loading="initialLoading">
      <template #card-uninvoiced>
        <UiStack>
          <UiHeading :level="2" variant="card">
            {{ uninvoicedTotalLabel
              ? t('clients.clientBillableItems.uninvoicedItemsAmountLength', { amount: uninvoicedTotalLabel, length: uninvoicedItems.length })
              : t('clients.clientBillableItems.uninvoicedItemsLength', { length: uninvoicedItems.length }) }}
          </UiHeading>
          <UiTable
            v-if="uninvoicedItems.length > 0"
            :density="UiTableDensity.Comfortable"
            :selection="selection"
            select-all-testid="client-billable-select-all"
            data-testid="client-billable-uninvoiced-table"
          >
            <UiSchemaTableHead :columns="UNINVOICED_ITEMS_TABLE.columns" />
            <UiTableBody divided>
              <UiTableRow
                v-for="item in uninvoicedItems"
                :key="item.id"
                hoverable
                :row-id="item.id"
                :select-testid="`client-billable-select-${item.id}`"
              >
                <UiTableCell muted>{{ item.id }}</UiTableCell>
                <UiTableCell>{{ item.description }}</UiTableCell>
                <UiTableCell muted numeric>{{ formatPlainAmount(item.amount, item.currency) }}</UiTableCell>
                <UiTableCell actions>
                  <UiRowActions :actions="billableItemActions(item)" :testid="`client-billable-actions-${item.id}`" />
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
          <UiEmptyState
            v-else
            :title="narrowed ? t('clients.clientBillableItems.noUninvoicedItemsMatchYourFilters') : t('clients.clientBillableItems.noUninvoicedBillableItems')"
            data-testid="client-billable-uninvoiced-empty"
          />
        </UiStack>
      </template>
      <template #card-invoiced>
        <UiTable v-if="invoicedItems.length > 0" :density="UiTableDensity.Comfortable" data-testid="client-billable-invoiced-table">
          <UiSchemaTableHead :columns="INVOICED_ITEMS_TABLE.columns" />
          <UiTableBody divided>
            <UiTableRow v-for="item in invoicedItems" :key="item.id" hoverable>
              <UiTableCell muted>{{ item.id }}</UiTableCell>
              <UiTableCell>{{ item.description }}</UiTableCell>
              <UiTableCell muted numeric>{{ formatPlainAmount(item.amount, item.currency) }}</UiTableCell>
              <UiTableCell>
                <template v-if="item.invoiceId">#{{ item.invoiceId }}</template>
                <template v-else>{{ EMPTY_VALUE }}</template>
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
        <UiEmptyState
          v-else
          :title="narrowed ? t('clients.clientBillableItems.noInvoicedItemsMatchYourFilters') : t('clients.clientBillableItems.noInvoicedBillableItems')"
          data-testid="client-billable-invoiced-empty"
        />
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
            :page="page"
            :total="invoicedTotalCount"
            :page-size="invoicedPageSize"
            testid="client-billable-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiDetailCards>

    <UiBulkBar
      :count="selection.count.value"
      :actions="bulkActions"
      data-testid="client-billable-bulk"
      @clear="selection.clear"
      @action="runBulkAction"
    />

    <UiDialog
      v-if="showInvoiceConfirmModal"
      variant="confirm"
      :title="t('clients.clientBillableItems.confirmInvoice')"
      :message="t('clients.clientBillableItems.areYouSureYouWant')"
      :confirm-label="t('shared.ok')"
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
        : t('clients.clientBillableItems.confirmDeleteSelected', { count: selection.count.value }, selection.count.value)"
      :confirm-label="t('shared.delete')"
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
