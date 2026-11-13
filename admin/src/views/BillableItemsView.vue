<script setup lang="ts">
/**
 * Billable items: the uninvoiced and recurring charges of every client, narrowed as the operator
 * types by client, description, amount and status, paged on the page, with a confirmed bulk
 * delete of the selected rows (`useRowSelection`, the floating `UiBulkBar`). The outcome of the delete (success, or the API's sentence) shows above
 * the table; a refused load shows the sentence with a Retry.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiBulkBar from '@/components/ui/UiBulkBar.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
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
import { useClientOptions } from '@/composables/useClientOptions'
import { useResultBanner } from '@/composables/useResultBanner'
import { useRowSelection } from '@/composables/useRowSelection'
import { useBillableItemsStore } from '@/stores/billableItems'
import { pageSlice } from '@/lib/paging'
import { BillableItemType } from '@/types/billableitem'
import type { BillableItem } from '@/types/billableitem'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { AppRoute } from '@/lib/routes'
import { formatPlainAmount } from '@/lib/money'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** What the status filter can single out. */
const ItemStatus = {
  Any: 'any',
  Uninvoiced: 'uninvoiced',
  Invoiced: 'invoiced',
  Recurring: 'recurring',
  ActiveRecurring: 'active-recurring',
  CompletedRecurring: 'completed-recurring',
} as const

const store = useBillableItemsStore()
const { clients, load: loadClients } = useClientOptions()

/** Current page (1-based). */
const page = ref(1)

/** True while the filter card is open. */
const filtersOpen = ref(false)

/** Client filter, as the picker holds the id; empty means every client. */
const filterClientId = ref('')

/** Description filter. */
const filterDescription = ref('')

/** Amount filter; 0 means any. */
const filterAmount = ref(0)

/** Status filter. */
const filterStatus = ref<string>(ItemStatus.Any)

/** True while the bulk-delete confirmation is open. */
const deleteOpen = ref(false)

/** True while the bulk delete runs. */
const deleting = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last bulk delete. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Loads the items and records whether the load was refused.
 *
 * @returns Resolves when the list has settled.
 */
const load = async (): Promise<void> => {
  await store.fetchAll()
  loadError.value = store.error
}

/** Options of the status select. */
const statusOptions = computed(() => [
  { value: ItemStatus.Any, label: t('shared.any') },
  { value: ItemStatus.Uninvoiced, label: t('billing.billableItems.uninvoiced') },
  { value: ItemStatus.Invoiced, label: t('billing.billableItems.invoiced') },
  { value: ItemStatus.Recurring, label: t('orders.orderDetail.recurring') },
  { value: ItemStatus.ActiveRecurring, label: t('billing.billableItems.activeRecurring') },
  { value: ItemStatus.CompletedRecurring, label: t('billing.billableItems.completedRecurring') },
])

/**
 * The name of a billable item's type, as the operator reads it.
 *
 * @param type - The type the API sent.
 * @returns The translated name.
 */
const typeLabel = (type: string): string =>
  type === BillableItemType.Recurring ? t('billing.billableItems.typeRecurring') : t('billing.billableItems.typeOneTime')

/**
 * Tells whether an item passes the status filter.
 *
 * @param item - A billable item.
 * @returns True when the item has the chosen status.
 */
const hasStatus = (item: BillableItem): boolean => {
  const recurring = item.type === BillableItemType.Recurring
  switch (filterStatus.value) {
    case ItemStatus.Uninvoiced:
      return !item.isInvoiced
    case ItemStatus.Invoiced:
      return item.isInvoiced
    case ItemStatus.Recurring:
      return recurring
    case ItemStatus.ActiveRecurring:
      return recurring && !item.isInvoiced
    case ItemStatus.CompletedRecurring:
      return recurring && item.isInvoiced
    default:
      return true
  }
}

/** Every loaded item that passes the filter card. */
const filteredItems = computed<BillableItem[]>(() =>
  store.items.filter((item) => {
    if (filterClientId.value !== '' && String(item.clientId) !== filterClientId.value) {
      return false
    }
    if (filterDescription.value !== '' && !item.description.toLowerCase().includes(filterDescription.value.toLowerCase())) {
      return false
    }
    if (filterAmount.value > 0 && item.amount !== filterAmount.value) {
      return false
    }
    return hasStatus(item)
  }),
)

/** The page of filtered items on show. */
const pageItems = computed<BillableItem[]>(() =>
  pageSlice(filteredItems.value, page.value, DEFAULT_PAGE_SIZE),
)

/** The selected rows of the page. */
const selection = useRowSelection(() => pageItems.value.map((item) => item.id))

/** Clears every filter. */
const clearFilters = (): void => {
  filterClientId.value = ''
  filterDescription.value = ''
  filterAmount.value = 0
  filterStatus.value = ItemStatus.Any
}

/**
 * Deletes the selected rows one by one (the store reloads the list after each), stopping at the
 * first refusal, and shows the outcome.
 *
 * @returns Resolves when the deletes have settled.
 */
const deleteSelected = async (): Promise<void> => {
  feedback.clear()
  deleting.value = true
  let refused = false
  for (const id of selection.ids.value) {
    if (!(await store.removeItem(id))) {
      refused = true
      break
    }
  }
  deleting.value = false
  deleteOpen.value = false
  selection.clear()
  if (refused) {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  } else {
    feedback.show(t('billing.billableItems.itemsDeleted'), false)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteOpen.value = false
  }
}

/** Filtering from the first page again, with nothing selected, so a bulk delete never takes rows the filter hides. */
watch([filterClientId, filterDescription, filterAmount, filterStatus], () => {
  page.value = 1
  selection.clear()
})

/** Loads the items and the client picker's options. */
onMounted(() => {
  void load()
  void loadClients()
})
</script>

<template>
  <UiStack data-testid="billing-billable-items-view">
    <UiPageHeader :title="t('billing.billableItems.billableItems')" :subtitle="t('billing.billableItems.manageUninvoicedAndRecurringCharges')">
      <template #actions>
        <UiButton variant="secondary" data-testid="billing-billable-items-filter-toggle" @click="filtersOpen = !filtersOpen">
          {{ filtersOpen ? t('shared.hideFilters') : t('shared.searchFilter') }}
        </UiButton>
        <UiLink :to="AppRoute.BillingBillableItemsAdd" variant="primary" data-testid="billing-billable-items-add">
          {{ t('shared.addItem') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiFilterCard v-if="filtersOpen">
      <UiGrid :columns="2">
        <UiField :label="t('shared.client')">
          <UiClientSelect
            v-model="filterClientId"
            :clients="clients"
            :placeholder="t('shared.startTypingToSearchClients')"
          />
        </UiField>
        <UiField :label="t('shared.description')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="filterDescription"
              type="text"
              :placeholder="t('billing.billableItems.searchDescription')"
              data-testid="billing-billable-items-filter-description"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.amount')">
          <UiNumberInput v-model="filterAmount" :step="0.01" :min="0" placeholder="0.00" />
        </UiField>
        <UiField :label="t('shared.status')">
          <UiSelect v-model="filterStatus" :options="statusOptions" :placeholder="t('shared.status')" />
        </UiField>
      </UiGrid>
      <template #actions>
        <UiButton variant="ghost" data-testid="billing-billable-items-clear" @click="clearFilters">
          {{ t('shared.clear') }}
        </UiButton>
      </template>
    </UiFilterCard>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-billable-items-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-billable-items-error"
      @retry="load"
    />

    <UiText tone="muted" size="md" data-testid="billing-billable-items-count">
      {{ t('billing.billableItems.lengthRecordsFound', { length: filteredItems.length }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && store.items.length === 0"
        :empty="!store.loading && pageItems.length === 0"
        :selection="selection"
        select-all-testid="billing-billable-items-select-all"
        data-testid="billing-billable-items-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.amount') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.billableItems.invoiced') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="item in pageItems"
            :key="item.id"
            hoverable
            :row-id="item.id"
            :select-testid="`billing-billable-item-select-${item.id}`"
          >
            <UiTableCell muted>{{ item.id }}</UiTableCell>
            <UiTableCell>{{ item.clientName }}</UiTableCell>
            <UiTableCell>{{ item.description }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(item.amount, item.currency) }}</UiTableCell>
            <UiTableCell muted>{{ typeLabel(item.type) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="item.isInvoiced ? 'success' : 'danger'">
                {{ item.isInvoiced ? t('shared.yes') : t('shared.no') }}
              </UiBadge>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('billing.billableItems.noItemsFound') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      v-model:page="page"
      :total="filteredItems.length"
      :page-size="DEFAULT_PAGE_SIZE"
      testid="billing-billable-items-pagination"
    />

    <UiBulkBar :count="selection.count.value" data-testid="billing-billable-items-bulk" @clear="selection.clear">
      <UiButton variant="danger" size="sm" data-testid="billing-billable-items-delete" @click="deleteOpen = true">
        {{ t('shared.delete') }}
      </UiButton>
    </UiBulkBar>

    <UiDialog
      variant="confirm"
      :open="deleteOpen"
      :title="t('billing.billableItems.deleteItems')"
      :message="t('billing.billableItems.deleteItemsMessage', { count: selection.count.value }, selection.count.value)"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      tone="danger"
      @confirm="deleteSelected"
      @close="dismissDelete"
    />
  </UiStack>
</template>
