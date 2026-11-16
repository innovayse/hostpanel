<script setup lang="ts">
/**
 * Admin VAT Rates settings view: a search box on the country code and a paged table of the VAT rates of the EU member
 * states (country, kind, rate and the day it applies from) with add, edit and delete.
 *
 * The search text and the page live in the address (`useListQuery`), so a reload, a shared link and the back button
 * restore the same list, and the API applies the search before it pages. The add and edit dialog shows a refused save
 * inside itself; a result banner reports a delete, which a confirmation dialog guards. A save or a delete reads the
 * page the address names again.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import VatRateFormModal from '@/components/settings/VatRateFormModal.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useListQuery } from '@/composables/useListQuery'
import { useResultBanner } from '@/composables/useResultBanner'
import { formatDate } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { reportCountryName } from '@/lib/reportsCountry'
import { useVatRatesStore } from '@/stores/vatRates'
import { VatRateKind } from '@/types/vatrate'
import type { SaveVatRatePayload, VatRateDto } from '@/types/vatrate'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTextSize } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t, locale } = useI18n()

/** VAT rates store: the page, the loading flag and the refusal sentences. */
const store = useVatRatesStore()

/** The search text and the page of the list, kept in the address. */
const { search, appliedSearch, page, setPage, narrowed, loadOnChange, stepBackWhenEmpty }
  = useListQuery([])

/**
 * Loads the page the address names with the search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, appliedSearch.value)

// Every change of the search or the page loads the list the address names.
loadOnChange(load)

/** Whether the current page has any row to list. */
const hasRows = computed<boolean>(() => store.vatRates.length > 0)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && !hasRows.value)

/** The result banner for a delete. */
const banner = useResultBanner()

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
  <UiStack gap="none" data-testid="vat-rates-view">
    <UiPageHeader :title="t('vatRates.title')" :subtitle="t('vatRates.description')">
      <template #actions>
        <UiButton type="button" variant="primary" data-testid="vat-rates-add" @click="openCreate">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('vatRates.addRate') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiAlert
      v-if="banner.message.value"
      :variant="banner.isError.value ? UiTone.Danger : UiTone.Success"
      class="mb-5"
      data-testid="vat-rates-result"
    >
      {{ banner.message.value }}
    </UiAlert>

    <UiListToolbar
      v-model:search="search"
      search-testid="vat-rates-search"
      :search-placeholder="t('vatRates.searchPlaceholder')"
      :loading="initialLoading"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="vat-rates-error"
      @retry="load"
    />

    <UiCard v-else-if="hasRows || store.loading" class="overflow-hidden">
      <UiTable :density="UiTableDensity.Comfortable" :loading="initialLoading" data-testid="vat-rates-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader align="left">{{ t('vatRates.table.country') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('vatRates.table.kind') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('vatRates.table.rate') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('vatRates.table.validFrom') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('vatRates.table.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="rate in store.vatRates"
            :key="rate.id"
            hoverable
            :data-testid="`vat-rate-row-${rate.id}`"
          >
            <UiTableCell class="font-semibold text-text-primary">{{ reportCountryName(rate.country, locale) }}</UiTableCell>
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
      </UiTable>

      <template #footer>
        <UiStack direction="row" justify="between" align="center" wrap>
          <UiResultCount :loading="initialLoading" :size="UiTextSize.Xs">
            {{ t('vatRates.totalCount', { totalCount: store.totalCount }) }}
          </UiResultCount>
          <UiPagination
            :page="page"
            :total="store.totalCount"
            :page-size="store.pageSize"
            testid="vat-rates-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="narrowed ? t('vatRates.noMatches') : t('vatRates.empty')"
        data-testid="vat-rates-empty"
      >
        <template #icon>
          <UiIcon :markup="Icon.Flag" size="lg" />
        </template>
      </UiEmptyState>
    </UiCard>

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
      variant="confirm"
      :title="t('vatRates.deleteModal.title')"
      :message="t('vatRates.deleteModal.message', {
        kind: t(`vatRates.kind.${deleteTarget.kind}`),
        country: reportCountryName(deleteTarget.country, locale),
        validFrom: formatDate(deleteTarget.validFrom),
      })"
      :confirm-label="t('vatRates.deleteModal.confirmLabel')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
