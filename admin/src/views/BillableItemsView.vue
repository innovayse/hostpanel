<script setup lang="ts">
/**
 * Billable items: the uninvoiced and recurring charges of every client, searched by description or id, narrowed by
 * client, type and invoiced state, and paged by the API, with a confirmed bulk delete of the selected rows
 * (`useRowSelection`, the floating `UiBulkBar`).
 *
 * The search text, the filters and the page live in the address (`useListQuery`): the filter button opens the client,
 * type and invoiced fields, an applied filter shows as a removable chip, and a reload or a shared link restores the
 * same list, which the API narrows before it pages, so every item is reachable however many there are. The client is
 * chosen from the picker, not typed, because only the Clients module can read client names. The outcome of the delete
 * (success, or the API's sentence) shows above the table; a refused load shows the sentence with a Retry.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BillableItemTypeField from '@/components/billing/BillableItemTypeField.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiBulkBar from '@/components/ui/UiBulkBar.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusBadge from '@/components/ui/UiStatusBadge.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/BillableItemsView.schema'
import { useClientOptions } from '@/composables/useClientOptions'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useRowSelection } from '@/composables/useRowSelection'
import { useBillableItemsStore } from '@/stores/billableItems'
import { BILLABLE_ITEM_TYPE_OPTIONS } from '@/lib/constants'
import { statusLabel } from '@/lib/tones'
import { BillableItemInvoiced, BillableItemType } from '@/types/billableitem'
import type { BillableItemListFilters } from '@/types/billableitem'
import { AppRoute } from '@/lib/routes'
import { Icon } from '@/lib/icons'
import type { UiBulkAction } from '@/utils/ui/bulkBar'
import { formatPlainAmount } from '@/lib/money'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  headerAction(UiActionKind.Add, { label: t('billing.billableItems.create'), variant: UiButtonVariant.Primary, testid: 'billing-billable-items-add', to: AppRoute.BillingBillableItemsAdd }),
])

const store = useBillableItemsStore()
const { clients, load: loadClients } = useClientOptions()

/** The filter fields the dialog edits; each is one query key and one `BillableItemListFilters` member. */
const FilterKey = {
  ClientId: 'clientId',
  Type: 'type',
  Invoiced: 'invoiced',
} as const

/** The search text, the filters and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, idOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The client in the address as an id, undefined when none is set or the value is not an id. */
const clientIdOfAddress = idOf(FilterKey.ClientId)

/** The type in the address as a billable item type, undefined when none is set or the value is not one. */
const typeOfAddress = enumOf(FilterKey.Type, BillableItemType)

/** The invoiced filter in the address, undefined when none is set or the value is not one of its two. */
const invoicedOfAddress = enumOf(FilterKey.Invoiced, BillableItemInvoiced)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<BillableItemListFilters>(() => ({
  search: appliedSearch.value,
  clientId: clientIdOfAddress.value,
  type: typeOfAddress.value,
  invoiced: invoicedOfAddress.value,
}))

/** Choices of the invoiced field; the empty value means any. */
const invoicedOptions = computed(() => [
  { value: '', label: t('shared.any') },
  { value: BillableItemInvoiced.Yes, label: t('shared.yes') },
  { value: BillableItemInvoiced.No, label: t('shared.no') },
])

/** The applied client, type and invoiced state as removable chips, none while the list is not filtered. */
const chips = chipsOf(() => [
  {
    key: FilterKey.ClientId,
    label: t('shared.client'),
    value: clientIdOfAddress.value === undefined
      ? undefined
      : (clients.value.find(client => client.id === clientIdOfAddress.value)?.name ?? `#${clientIdOfAddress.value}`),
  },
  {
    key: FilterKey.Type,
    label: t('shared.type'),
    value: typeOfAddress.value === undefined ? undefined : statusLabel(BILLABLE_ITEM_TYPE_OPTIONS, typeOfAddress.value),
  },
  {
    key: FilterKey.Invoiced,
    label: t('billing.billableItems.invoiced'),
    value: invoicedOfAddress.value === undefined ? undefined : statusLabel(invoicedOptions.value, invoicedOfAddress.value),
  },
])

/** True while the bulk-delete confirmation is open. */
const deleteOpen = ref(false)

/** The one bulk action of the list: delete the selected items (after confirmation). */
const bulkActions = computed<UiBulkAction[]>(() => [
  { key: 'delete', label: t('shared.delete'), title: t('shared.delete'), icon: Icon.Trash, variant: UiButtonVariant.Danger, testid: 'billing-billable-items-delete' },
])

/** True while the bulk delete runs. */
const deleting = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last bulk delete. */
const feedback = useOutcomeToast()

/**
 * Loads the page the address names with the filters and search it holds, and records whether the load was refused.
 *
 * @returns Resolves when the list has settled.
 */
const load = async (): Promise<void> => {
  const loaded = await store.fetchAll(page.value, filters.value)
  loadError.value = loaded ? null : store.error
}

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && store.items.length === 0)

/** The selected rows of the page. */
const selection = useRowSelection(() => store.items.map((item) => item.id))

/**
 * The name of a billable item's type, as the operator reads it.
 *
 * @param type - The type the API sent.
 * @returns The translated name.
 */
const typeLabel = (type: string): string => statusLabel(BILLABLE_ITEM_TYPE_OPTIONS, type)

/**
 * Deletes the selected rows one by one, stopping at the first refusal, reloads the page and shows the outcome.
 *
 * @returns Resolves when the deletes have settled and the list has been refreshed.
 */
const deleteSelected = async (): Promise<void> => {
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
  // The refusal is read before the reload, because the reload starts by forgetting the store's last sentence.
  const refusal = store.error ?? t('errors.noAnswer')
  await load()
  if (refused) {
    feedback.show(refusal, true)
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

/** Loads the client picker's options; the list itself loads through `loadOnChange`. */
onMounted(() => {
  void loadClients()
})
</script>

<template>
  <UiStack data-testid="billing-billable-items-view">
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="billing-billable-items-search"
      :search-placeholder="t('billing.billableItems.searchItems')"
      filter-testid="billing-billable-items-filter-toggle"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiField :label="t('shared.client')">
        <UiClientSelect
          v-model="draft.clientId"
          :clients="clients"
          :placeholder="t('shared.startTypingToSearchClients')"
          data-testid="billing-billable-items-filter-client"
        />
      </UiField>
      <BillableItemTypeField v-model="draft.type" testid="billing-billable-items-filter-type" />
      <UiField :label="t('billing.billableItems.invoiced')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="draft.invoiced"
            :options="invoicedOptions"
            :placeholder="t('shared.any')"
            data-testid="billing-billable-items-filter-invoiced"
          />
        </template>
      </UiField>
    </UiListToolbar>


    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-billable-items-error"
      @retry="load"
    />

    <UiDataTable
      v-if="loadError === null"
      :loading="initialLoading"
      :empty="!store.loading && store.items.length === 0"
      :selection="selection"
      select-all-testid="billing-billable-items-select-all"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="billing-billable-items-pagination"
      data-testid="billing-billable-items-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="item in store.items"
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
            <UiStatusBadge
              :state="item.isInvoiced"
              :on-label="t('shared.yes')"
              :off-label="t('shared.no')"
            />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState
          :title="narrowed ? t('billing.billableItems.noItemsMatchYourFilters') : t('billing.billableItems.noItemsFound')"
          data-testid="billing-billable-items-empty"
        >
          <template v-if="activeCount > 0" #actions>
            <UiButton :variant="UiButtonVariant.Link" data-testid="billing-billable-items-clear" @click="clearAll">
              {{ t('shared.clearFilters') }}
            </UiButton>
          </template>
        </UiEmptyState>
      </template>
    </UiDataTable>

    <UiBulkBar
      :count="selection.count.value"
      :actions="bulkActions"
      data-testid="billing-billable-items-bulk"
      @clear="selection.clear"
      @action="deleteOpen = true"
    />

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="deleteOpen"
      :title="t('billing.billableItems.deleteItems')"
      :message="t('billing.billableItems.deleteItemsMessage', { count: selection.count.value }, selection.count.value)"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="deleteSelected"
      @close="dismissDelete"
    />
  </UiStack>
</template>
