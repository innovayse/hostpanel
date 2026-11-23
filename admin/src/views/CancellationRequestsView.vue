<script setup lang="ts">
/**
 * Cancellation requests: a search box, the type and status filters and a paged table of the service cancellation
 * requests.
 *
 * The search text (matched against the reason and the request id), the filters and the page live in the address
 * (`useListQuery`): the filter button opens the type and status fields, an applied filter shows as a removable chip,
 * and a reload or a shared link restores the same list, which the API narrows before it pages. Shows date,
 * product/service, client, reason, type, status and a delete action that asks first. Reads and writes
 * `stores/cancellationRequests`. The outcome of a delete (success, or the API's sentence) shows above the table; a
 * refused load shows the sentence with a Retry.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/CancellationRequestsView.schema'
import { CancellationStatus, CancellationType } from '@/types/service'
import type { CancellationRequestFilters, CancellationRequestItem } from '@/types/service'
import UiText from '@/components/ui/UiText.vue'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { cancellationStatusLabel, cancellationStatusTone, cancellationTypeLabel, cancellationTypeTone } from '@/lib/tones'
import { useCancellationRequestsStore } from '@/stores/cancellationRequests'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiSize } from '@/utils/ui/size'
import { UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { clientPath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'

const { t } = useI18n()

/** Cancellation requests store: the page and the load state. */
const store = useCancellationRequestsStore()

/** The filter fields the dialog edits; each is one query key and one `CancellationRequestFilters` member. */
const FilterKey = {
  Type: 'type',
  Status: 'status',
} as const

/** The search text, the filters and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The type in the address as a cancellation type, undefined when none is set or the value is not one. */
const typeOfAddress = enumOf(FilterKey.Type, CancellationType)

/** The status in the address as a cancellation status, undefined when none is set or the value is not one. */
const statusOfAddress = enumOf(FilterKey.Status, CancellationStatus)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<CancellationRequestFilters>(() => ({
  search: appliedSearch.value,
  type: typeOfAddress.value,
  status: statusOfAddress.value,
}))

/** Choices of the type field; the empty value means any. */
const typeOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(CancellationType).map(type => ({ value: type, label: cancellationTypeLabel(type) })),
])

/** Choices of the status field; the empty value means any. */
const statusOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(CancellationStatus).map(status => ({ value: status, label: cancellationStatusLabel(status) })),
])

/** The applied type and status as removable chips, none while the list is not filtered. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Type,
    label: t('shared.type'),
    value: typeOfAddress.value === undefined ? undefined : cancellationTypeLabel(typeOfAddress.value),
  },
  {
    key: FilterKey.Status,
    label: t('shared.status'),
    value: statusOfAddress.value === undefined ? undefined : cancellationStatusLabel(statusOfAddress.value),
  },
])

/** Longest reason shown in the table before it is cut with an ellipsis. */
const REASON_MAX_LENGTH = 60

/** ID of the request awaiting delete confirmation, null when nothing is staged. */
const deleteTarget = ref<number | null>(null)

/** True while the staged request is being deleted. */
const deleting = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last delete. */
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

/** Whether the current page has any request to list. */
const hasRows = computed<boolean>(() => store.cancellations.length > 0)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && !hasRows.value)

/**
 * Shortens a reason for the table row, keeping the full text for the tooltip.
 *
 * @param text - The reason as the client wrote it.
 * @returns The text, cut with an ellipsis when longer than the limit.
 */
const shorten = (text: string): string =>
  text.length <= REASON_MAX_LENGTH ? text : `${text.slice(0, REASON_MAX_LENGTH)}…`

/**
 * Stages a cancellation request for deletion. Only opens the prompt; {@link handleDelete} does the
 * work, so the question is asked by the app's own dialog rather than the browser's.
 *
 * @param id - The cancellation request ID to delete.
 */
const confirmDelete = (id: number): void => {
  deleteTarget.value = id
}

/**
 * Deletes the staged cancellation request, clears the prompt, reloads the page and shows the outcome.
 *
 * @returns Promise that resolves when the delete attempt has finished and the list has been refreshed.
 */
const handleDelete = async (): Promise<void> => {
  const id = deleteTarget.value
  if (id === null) {
    return
  }
  deleting.value = true
  const deleted = await store.deleteRequest(id)
  deleting.value = false
  deleteTarget.value = null
  if (deleted) {
    await load()
    feedback.show(t('clients.cancellationRequests.requestDeleted'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTarget.value = null
  }
}

/**
 * The row actions of a cancellation request: one, delete it (confirmed first).
 *
 * @param item - The row.
 * @returns The action list passed to UiRowActions.
 */
const cancellationActions = (item: CancellationRequestItem): UiMenuAction[] => [
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    testid: `cancellations-delete-${item.id}`,
    /**
     * Asks for confirmation, then deletes this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      confirmDelete(item.id)
    },
    danger: true,
  },
]
</script>

<template>
  <UiStack>
    <UiPageHeader
      v-bind="schemaHeader(schema, t)"
      data-testid="cancellations-header"
    />

    <UiListToolbar
      v-model:search="search"
      search-testid="cancellations-search"
      :search-placeholder="t('clients.cancellationRequests.searchRequests')"
      filter-testid="cancellations-filter"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiField :label="t('shared.type')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="draft.type"
            :options="typeOptions"
            :placeholder="t('shared.any')"
            data-testid="cancellations-filter-type"
          />
        </template>
      </UiField>
      <UiField :label="t('shared.status')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="draft.status"
            :options="statusOptions"
            :placeholder="t('shared.any')"
            data-testid="cancellations-filter-status"
          />
        </template>
      </UiField>
    </UiListToolbar>


    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="cancellations-error"
      @retry="load"
    />

    <UiDataTable
      v-if="loadError === null"
      :loading="initialLoading"
      :empty="!store.loading && !hasRows"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="cancellations-pagination"
      data-testid="cancellations-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="item in store.cancellations"
          :key="item.id"
          hoverable
          :data-testid="`cancellations-row-${item.id}`"
        >
          <UiTableCell muted>{{ formatDate(item.createdAt) }}</UiTableCell>
          <UiTableCell>{{ item.serviceName }}</UiTableCell>
          <UiTableCell>
            <UiLink :variant="UiButtonVariant.Link" :to="clientPath(item.clientId)" :data-testid="`cancellations-client-${item.id}`">
              {{ item.clientName }}
            </UiLink>
          </UiTableCell>
          <UiTableCell>
            <UiText :tone="UiTextTone.Secondary" :title="item.reason ?? ''">
              {{ item.reason ? shorten(item.reason) : EMPTY_VALUE }}
            </UiText>
          </UiTableCell>
          <UiTableCell>
            <UiBadge :tone="cancellationTypeTone(item.type)" small>{{ cancellationTypeLabel(item.type) }}</UiBadge>
          </UiTableCell>
          <UiTableCell>
            <UiBadge :tone="cancellationStatusTone(item.status)" small>
              {{ cancellationStatusLabel(item.status) }}
            </UiBadge>
          </UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="cancellationActions(item)" :testid="`cancellations-actions-${item.id}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState
          :title="narrowed ? t('clients.cancellationRequests.noRequestsMatchYourFilters') : t('clients.cancellationRequests.noCancellationRequestsFound')"
          data-testid="cancellations-empty"
        >
          <template #icon>
            <UiIcon :markup="Icon.Remove" :size="UiSize.Lg" />
          </template>
          <template v-if="activeCount > 0" #actions>
            <UiButton :variant="UiButtonVariant.Link" data-testid="cancellations-clear-filters" @click="clearAll">
              {{ t('shared.clearFilters') }}
            </UiButton>
          </template>
        </UiEmptyState>
      </template>
    </UiDataTable>

    <UiDialog
      v-if="deleteTarget !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('clients.cancellationRequests.deleteCancellationRequest')"
      :message="t('clients.cancellationRequests.areYouSureYouWant')"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      data-testid="cancellations-delete-dialog"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
