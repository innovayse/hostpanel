<script setup lang="ts">
/**
 * Admin VAT Rates settings view: a search box on the country code, filters on the kind of rate and on whether the row is
 * in force, superseded or upcoming, and a paged table of the VAT rates of the EU member states (country, kind, rate and
 * the day it applies from) with add, edit and delete.
 *
 * The search text, the filters and the page live in the address (`useListQuery`), so a reload, a shared link and the
 * back button restore the same list, and the API applies them before it pages. The add and edit dialog shows a refused
 * save inside itself; a delete is confirmed by a dialog and answered by a toast, and a refusal by a banner. A save or a
 * delete reads the page the address names again.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import VatRateFormModal from '@/components/settings/VatRateFormModal.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { formatDate } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { reportCountryName } from '@/lib/reportsCountry'
import { schemaHeader } from '@/lib/schemaHeader'
import { useVatRatesStore } from '@/stores/vatRates'
import { VatRateKind, VatRateStatus } from '@/types/vatrate'
import type { SaveVatRatePayload, VatRateDto, VatRateListFilters } from '@/types/vatrate'
import { UiTableDensity } from '@/utils/ui/injection'
import schema from '@/views/VatRatesView.schema'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTone } from '@/utils/ui/tone'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiSize } from '@/utils/ui/size'
import { UiActionKind } from '@/utils/ui/actions'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t, locale } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  headerAction(UiActionKind.Add, { label: t('vatRates.addRate'), variant: UiButtonVariant.Primary, testid: 'vat-rates-add', onSelect: openCreate }),
])

/** VAT rates store: the page, the loading flag and the refusal sentences. */
const store = useVatRatesStore()

/** The filter fields the dialog edits; each is one query key and one `VatRateListFilters` member. */
const FilterKey = {
  Kind: 'kind',
  Status: 'status',
} as const

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, stepBackWhenEmpty, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Kind filter options; the empty value means any. */
const kindOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(VatRateKind).map((kind) => ({ value: kind, label: t(`vatRates.kind.${kind}`) })),
])

/** Status filter options; the empty value means any. */
const statusOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(VatRateStatus).map((status) => ({ value: status, label: t(`vatRates.status.${status}`) })),
])

/** The kind in the address as a kind of rate, undefined when none is set or the value is not one. */
const kindOfAddress = enumOf(FilterKey.Kind, VatRateKind)

/** The status in the address as a standing, undefined when none is set or the value is not one. */
const statusOfAddress = enumOf(FilterKey.Status, VatRateStatus)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<VatRateListFilters>(() => ({
  search: appliedSearch.value,
  kind: kindOfAddress.value,
  status: statusOfAddress.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Kind,
    label: t('vatRates.table.kind'),
    value: kindOfAddress.value === undefined ? undefined : t(`vatRates.kind.${kindOfAddress.value}`),
  },
  {
    key: FilterKey.Status,
    label: t('vatRates.filters.status'),
    value: statusOfAddress.value === undefined ? undefined : t(`vatRates.status.${statusOfAddress.value}`),
  },
])

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, filters.value)

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

/** Whether the current page has any row to list. */
const hasRows = computed<boolean>(() => store.vatRates.length > 0)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && !hasRows.value)

/** The result banner for a delete. */
const banner = useOutcomeToast()

/** Whether the add/edit dialog is visible. */
const showModal = ref(false)

/** Row being edited, or null for add mode. */
const editingRate = ref<VatRateDto | null>(null)

/** True while a save is in flight. */
const saving = ref(false)

/** The row pending a delete confirmation, or null when the dialog is closed. */
const deleteTarget = ref<VatRateDto | null>(null)

/** True while a delete request is in flight. */
const deleting = ref(false)

/** Opens the dialog in add mode. */
const openCreate = (): void => {
  store.actionError = null
  editingRate.value = null
  showModal.value = true
}

/**
 * Opens the dialog in edit mode for the given row.
 *
 * @param rate - The row to edit.
 */
const openEdit = (rate: VatRateDto): void => {
  store.actionError = null
  editingRate.value = rate
  showModal.value = true
}

/**
 * Saves the row from the dialog (a new one, or the edited one), closes the dialog and reloads the page on success.
 *
 * @param payload - The row's fields.
 */
const handleSave = async (payload: SaveVatRatePayload): Promise<void> => {
  saving.value = true
  const saved = editingRate.value === null
    ? await store.create(payload)
    : await store.update(editingRate.value.id, payload)
  saving.value = false
  if (saved) {
    showModal.value = false
    await load()
  }
}

/**
 * Deletes the row the dialog asked about, reports the outcome and reloads the page. When the delete emptied the last
 * page, the list steps back to the page before it.
 */
const confirmDelete = async (): Promise<void> => {
  if (deleteTarget.value === null) {
    return
  }
  deleting.value = true
  const removed = await store.remove(deleteTarget.value.id)
  deleting.value = false
  deleteTarget.value = null
  if (removed) {
    banner.show(t('vatRates.toasts.deleted'), false)
    await load()
    await stepBackWhenEmpty(!hasRows.value)
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTarget.value = null
  }
}

/**
 * The row actions of a rate: edit, and delete last (it opens the confirmation).
 *
 * @param rate - The row.
 * @returns The action list passed to UiRowActions.
 */
const rateActions = (rate: VatRateDto): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    icon: Icon.Edit,
    testid: `vat-rate-edit-${rate.id}`,
    /**
     * Opens the edit dialog for this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      openEdit(rate)
    },
  },
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    testid: `vat-rate-delete-${rate.id}`,
    danger: true,
    /**
     * Stages this row for the delete confirmation.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      deleteTarget.value = rate
    },
  },
]
</script>

<template>
  <UiStack data-testid="vat-rates-view">
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>


    <UiListToolbar
      v-model:search="search"
      search-testid="vat-rates-search"
      :search-placeholder="t('vatRates.searchPlaceholder')"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack>
        <UiField :label="t('vatRates.table.kind')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.kind"
              :options="kindOptions"
              :placeholder="t('shared.any')"
              data-testid="vat-rates-filter-kind"
            />
          </template>
        </UiField>
        <UiField :label="t('vatRates.filters.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.status"
              :options="statusOptions"
              :placeholder="t('shared.any')"
              data-testid="vat-rates-filter-status"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="vat-rates-error"
      @retry="load"
    />

    <UiDataTable
      v-else
      :density="UiTableDensity.Comfortable"
      :loading="initialLoading"
      :empty="!store.loading && !hasRows"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="vat-rates-pagination"
      data-testid="vat-rates-table"
      @update:page="setPage"
    >
        <UiSchemaTableHead :columns="schema.table.columns" />
        <UiTableBody>
          <UiTableRow
            v-for="rate in store.vatRates"
            :key="rate.id"
            hoverable
            :data-testid="`vat-rate-row-${rate.id}`"
          >
            <UiTableCell strong>{{ reportCountryName(rate.country, locale) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="rate.kind === VatRateKind.Standard ? UiTone.Success : UiTone.Info" small>
                {{ t(`vatRates.kind.${rate.kind}`) }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell numeric muted>{{ rate.ratePercent }}%</UiTableCell>
            <UiTableCell muted>{{ formatDate(rate.validFrom) }}</UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="rateActions(rate)" :testid="`vat-rate-actions-${rate.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      <template #empty>
        <UiEmptyState
          :title="narrowed ? t('vatRates.noMatches') : t('vatRates.empty')"
          data-testid="vat-rates-empty"
        >
          <template #icon>
            <UiIcon :markup="Icon.Flag" :size="UiSize.Lg" />
          </template>
        </UiEmptyState>
      </template>
    </UiDataTable>

    <!-- Add/edit VAT rate dialog -->
    <VatRateFormModal
      v-if="showModal"
      :rate="editingRate"
      :saving="saving"
      :error="store.actionError"
      @save="handleSave"
      @close="showModal = false"
    />

    <!-- Delete confirmation -->
    <UiDialog
      v-if="deleteTarget"
      :variant="UiDialogVariant.Confirm"
      :title="t('vatRates.deleteModal.title')"
      :message="t('vatRates.deleteModal.message', {
        kind: t(`vatRates.kind.${deleteTarget.kind}`),
        country: reportCountryName(deleteTarget.country, locale),
        validFrom: formatDate(deleteTarget.validFrom),
      })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
