<script setup lang="ts">
/**
 * Network issues list: a paged list of network issues with create, edit and delete actions, narrowed by a search box
 * (title, server or description) and by the filter dialog (status, type and priority) with its removable chips. The
 * search text, the filters and the page live in the address (`useListQuery`), so a reload, a shared link and the back
 * button restore the same list, and the API narrows the issues before it pages. A refused load offers a retry; a delete
 * is confirmed in a danger prompt, its outcome (success, or the API's sentence) shows above the table, and a delete
 * that empties the last page steps back to the page before it.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/NetworkIssuesView.schema'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { NETWORK_ISSUE_PRIORITY_OPTIONS, NETWORK_ISSUE_STATUS_OPTIONS, NETWORK_ISSUE_TYPE_OPTIONS } from '@/lib/constants'
import { formatDateTimeCompact } from '@/lib/format'
import { networkIssuePriorityTone, networkIssueStatusTone, statusLabel } from '@/lib/tones'
import { useNetworkIssuesStore } from '@/stores/networkIssues'
import { NetworkIssuePriority, NetworkIssueStatus, NetworkIssueType } from '@/types/networkissue'
import type { NetworkIssue, NetworkIssueListFilters } from '@/types/networkissue'
import { UiButtonVariant } from '@/utils/ui/button'
import { AppRoute, editPath } from '@/lib/routes'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import { Icon } from '@/lib/icons'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('support.networkIssueForm.createIssue'), icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: 'network-issues-create', to: AppRoute.SupportNetworkIssuesNew },
])

/** The filter fields the dialog edits; each is one query key and one `NetworkIssueListFilters` member. */
const FilterKey = {
  Status: 'status',
  Type: 'type',
  Priority: 'priority',
} as const

const store = useNetworkIssuesStore()

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, loadOnChange, stepBackWhenEmpty, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The status in the address as an issue status, undefined when none is set or the value is not one. */
const statusOfAddress = enumOf(FilterKey.Status, NetworkIssueStatus)

/** The type in the address as an issue type, undefined when none is set or the value is not one. */
const typeOfAddress = enumOf(FilterKey.Type, NetworkIssueType)

/** The priority in the address as an issue priority, undefined when none is set or the value is not one. */
const priorityOfAddress = enumOf(FilterKey.Priority, NetworkIssuePriority)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<NetworkIssueListFilters>(() => ({
  search: appliedSearch.value,
  status: statusOfAddress.value,
  type: typeOfAddress.value,
  priority: priorityOfAddress.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Status,
    label: t('shared.status'),
    value: statusOfAddress.value === undefined ? undefined : statusLabel(NETWORK_ISSUE_STATUS_OPTIONS, statusOfAddress.value),
  },
  {
    key: FilterKey.Type,
    label: t('shared.type'),
    value: typeOfAddress.value === undefined ? undefined : statusLabel(NETWORK_ISSUE_TYPE_OPTIONS, typeOfAddress.value),
  },
  {
    key: FilterKey.Priority,
    label: t('shared.priority'),
    value: priorityOfAddress.value === undefined ? undefined : statusLabel(NETWORK_ISSUE_PRIORITY_OPTIONS, priorityOfAddress.value),
  },
])

/** Status choices; the empty value means any. */
const statusOptions = computed(() => [{ value: '', label: t('shared.any') }, ...NETWORK_ISSUE_STATUS_OPTIONS])

/** Type choices; the empty value means any. */
const typeOptions = computed(() => [{ value: '', label: t('shared.any') }, ...NETWORK_ISSUE_TYPE_OPTIONS])

/** Priority choices; the empty value means any. */
const priorityOptions = computed(() => [{ value: '', label: t('shared.any') }, ...NETWORK_ISSUE_PRIORITY_OPTIONS])

/** ID of the issue waiting for delete confirmation; null when no dialog is open. */
const deleteTarget = ref<number | null>(null)

/** Whether a delete request is in flight. */
const deleting = ref(false)

/** The outcome line of the last delete. */
const feedback = useOutcomeToast()

/**
 * Loads the page the address names with the search and the filters it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, filters.value)

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

/**
 * Deletes the issue the dialog asked about, shows the outcome and reloads the page on success, stepping back a page
 * when the delete emptied the last one.
 *
 * @returns Resolves when the delete (and the reload) has settled.
 */
const handleDelete = async (): Promise<void> => {
  if (deleteTarget.value === null) {
    return
  }
  deleting.value = true
  const removed = await store.remove(deleteTarget.value)
  deleting.value = false
  deleteTarget.value = null
  if (removed) {
    feedback.show(t('shared.recordDeleted'), false)
    await load()
    await stepBackWhenEmpty(store.issues.length === 0)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTarget.value = null
  }
}

/**
 * The row actions of a network issue: edit as a real link to its page, and delete last (it opens the confirmation).
 *
 * @param issue - The network issue of the row.
 * @returns The action list passed to UiRowActions.
 */
const rowActions = (issue: NetworkIssue): UiMenuAction[] => {
  /**
   * Stages this network issue for the delete confirmation.
   *
   * @returns Nothing.
   */
  const onDelete = (): void => {
    deleteTarget.value = issue.id
  }
  return [
    { label: t('shared.edit'), icon: Icon.Edit, testid: `network-issues-${issue.id}-edit`, to: editPath(AppRoute.SupportNetworkIssues, issue.id) },
    { label: t('shared.delete'), icon: Icon.Trash, testid: `network-issues-${issue.id}-delete`, danger: true, onSelect: onDelete },
  ]
}
</script>

<template>
  <UiStack>
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)" >
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="network-issues-search"
      :search-placeholder="t('support.networkIssues.searchNetworkIssues')"
      filter-testid="network-issues-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack>
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.status"
              :options="statusOptions"
              :placeholder="t('shared.any')"
              data-testid="network-issues-filter-status"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.type')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.type"
              :options="typeOptions"
              :placeholder="t('shared.any')"
              data-testid="network-issues-filter-type"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.priority')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.priority"
              :options="priorityOptions"
              :placeholder="t('shared.any')"
              data-testid="network-issues-filter-priority"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>


    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="network-issues-error"
      @retry="load"
    />

    <UiDataTable
      v-else
      :loading="store.loading && store.issues.length === 0"
      :empty="!store.loading && store.issues.length === 0"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="network-issues-pagination"
      data-testid="network-issues-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="issue in store.issues"
          :key="issue.id"
          hoverable
          :data-testid="`network-issues-row-${issue.id}`"
        >
          <UiTableCell>{{ issue.title }}</UiTableCell>
          <UiTableCell muted>{{ statusLabel(NETWORK_ISSUE_TYPE_OPTIONS, issue.type) }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="networkIssuePriorityTone[issue.priority]">{{ statusLabel(NETWORK_ISSUE_PRIORITY_OPTIONS, issue.priority) }}</UiBadge>
          </UiTableCell>
          <UiTableCell>
            <UiBadge :tone="networkIssueStatusTone[issue.status]">{{ statusLabel(NETWORK_ISSUE_STATUS_OPTIONS, issue.status) }}</UiBadge>
          </UiTableCell>
          <UiTableCell muted>{{ formatDateTimeCompact(issue.startDate) }}</UiTableCell>
          <UiTableCell muted>{{ formatDateTimeCompact(issue.endDate) }}</UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="rowActions(issue)" :testid="`network-issues-${issue.id}-actions`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState :title="t('shared.noRecordsFound')" data-testid="network-issues-empty" />
      </template>
    </UiDataTable>

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="deleteTarget !== null"
      :title="t('support.networkIssues.deleteNetworkIssue')"
      :message="t('support.networkIssues.areYouSureYouWant')"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
