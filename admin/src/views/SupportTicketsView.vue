<script setup lang="ts">
/**
 * Support tickets list: a paged table of all tickets with priority and status badges, a search box,
 * a filter dialog (status, priority, department and flag) whose active choices show as removable chips,
 * flagging and bulk actions on the selected rows (`useRowSelection`, the floating `UiBulkBar`). The search, the
 * filters and the page live in the address (`useListQuery`), so a reload or a shared link shows the same list, and the
 * API applies them before it pages. A refused load offers a retry; a flag or bulk action confirms itself with a
 * toast and a refusal shows the API's sentence above the list; a bulk delete is confirmed first in a
 * danger prompt. The flag column shows nothing on an unflagged ticket until its row is hovered, and a filled warning
 * flag on a flagged one; the last reply is a relative date, or "No replies yet" for a ticket nobody answered.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import SupportTicketFilterFields from '@/components/support/SupportTicketFilterFields.vue'
import SupportTicketPriorityBadge from '@/components/support/SupportTicketPriorityBadge.vue'
import SupportTicketStatusBadge from '@/components/support/SupportTicketStatusBadge.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { useListQuery } from '@/composables/useListQuery'
import { TicketFilterKey, useTicketFilters } from '@/composables/useTicketFilters'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useRowSelection } from '@/composables/useRowSelection'
import { UiDialogVariant } from '@/utils/ui/dialog'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiBulkBar from '@/components/ui/UiBulkBar.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/SupportTicketsView.schema'
import { formatRelativeFromNow, EMPTY_VALUE } from '@/lib/format'
import { useSupportStore } from '@/stores/support'
import { TicketBulkAction, TicketFlagFilter } from '@/types/ticket'
import type { TicketListFilters } from '@/types/ticket'
import { Icon } from '@/lib/icons'
import type { UiBulkAction } from '@/utils/ui/bulkBar'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiSize } from '@/utils/ui/size'
import { toneTextClasses, UiTone } from '@/utils/ui/tone'
import { AppRoute, supportTicketPath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiActionKind } from '@/utils/ui/actions'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t, locale } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  headerAction(UiActionKind.Add, { label: t('shared.createTicket'), variant: UiButtonVariant.Primary, testid: 'support-tickets-open-new', to: AppRoute.SupportTicketsNew }),
])

const store = useSupportStore()

/** The filter fields the dialog edits: the ticket filters every ticket list has, and the flag. */
const FilterKey = { ...TicketFilterKey, Flagged: 'flagged' } as const

/** Bulk actions offered for the selected tickets. */
const bulkActions = computed<UiBulkAction[]>(() => [
  { key: TicketBulkAction.Close, label: t('shared.close'), title: t('shared.close'), icon: Icon.Check, variant: UiButtonVariant.Primary, disabled: bulkRunning.value, testid: `support-tickets-bulk-${TicketBulkAction.Close}` },
  { key: TicketBulkAction.Delete, label: t('shared.delete'), title: t('shared.delete'), icon: Icon.Trash, variant: UiButtonVariant.Danger, disabled: bulkRunning.value, testid: `support-tickets-bulk-${TicketBulkAction.Delete}` },
  { key: TicketBulkAction.Flag, label: t('support.supportTickets.flag'), title: t('support.supportTickets.flag'), icon: Icon.Flag, variant: UiButtonVariant.Secondary, disabled: bulkRunning.value, testid: `support-tickets-bulk-${TicketBulkAction.Flag}` },
  { key: TicketBulkAction.Unflag, label: t('support.supportTickets.unflag'), title: t('support.supportTickets.unflag'), icon: Icon.Remove, variant: UiButtonVariant.Secondary, disabled: bulkRunning.value, testid: `support-tickets-bulk-${TicketBulkAction.Unflag}` },
])

/** The address-backed filters, search text and page of the list. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, idOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The flag filter in the address, undefined when it is not set or the value is not the flagged choice. */
const flagOfAddress = enumOf(FilterKey.Flagged, TicketFlagFilter)

/** The applied status, priority and department, and their chips. */
const ticketFilters = useTicketFilters({ enumOf, idOf }, computed(() => store.departments))

/** Flag filter options; the empty value means any. */
const flagOptions = computed(() => [
  { value: '', label: t('shared.any') },
  { value: TicketFlagFilter.Flagged, label: t('constants.ticketFilterOptions.flagged') },
])

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<TicketListFilters>(() => ({
  ...ticketFilters.filters.value,
  search: appliedSearch.value,
  flagged: flagOfAddress.value === undefined ? undefined : true,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  ...ticketFilters.chipSources(),
  {
    key: FilterKey.Flagged,
    label: t('support.supportTickets.flag'),
    value: flagOfAddress.value === undefined ? undefined : t('constants.ticketFilterOptions.flagged'),
  },
])

/** The selected tickets of the page. */
const selection = useRowSelection(() => store.tickets.map((ticket) => ticket.id))

/** The outcome of the last flag or bulk action: a success is a toast, a refusal stays in the banner. */
const feedback = useOutcomeToast()

/** True while the bulk delete confirmation is open. */
const confirmingBulkDelete = ref(false)

/** True while a bulk action is in flight. */
const bulkRunning = ref(false)

/**
 * Shows the outcome of a change: the success sentence, or the API's refusal.
 *
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 */
const report = (ok: boolean, successKey: string): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/** True while the first load is in flight and there are no rows yet; the table then draws its placeholder rows. */
const initialLoading = computed<boolean>(() => store.loading && store.tickets.length === 0)

/**
 * Loads the page the address names with the filters and search it holds; the selection, which belonged to the
 * previous page, is dropped.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => {
  selection.clear()
  return store.fetchAll(page.value, filters.value)
}

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

onMounted(() => store.fetchDepartments())

/**
 * Flips the flag of one ticket and reloads on success.
 *
 * @param id - The ticket identifier.
 */
const handleToggleFlag = async (id: number): Promise<void> => {
  const toggled = await store.toggleFlag(id)
  report(toggled, 'shared.changesSavedSuccessfully')
  if (toggled) {
    await load()
  }
}

/**
 * Runs a bulk action on the selected tickets, shows its outcome and reloads on success.
 *
 * @param action - The action to run.
 * @returns Resolves when the action (and the reload) has settled.
 */
const runBulkAction = async (action: TicketBulkAction): Promise<void> => {
  if (selection.count.value === 0) {
    return
  }
  bulkRunning.value = true
  const done = await store.bulkAction([...selection.ids.value], action)
  bulkRunning.value = false
  report(done, action === TicketBulkAction.Delete ? 'shared.recordDeleted' : 'shared.changesSavedSuccessfully')
  if (done) {
    await load()
  }
}

/**
 * Starts a bulk action from its button; a delete asks for confirmation first.
 *
 * @param action - The action to run.
 * @returns Resolves when the action has settled, or at once when it waits for confirmation.
 */
const handleBulkAction = async (action: TicketBulkAction): Promise<void> => {
  if (action === TicketBulkAction.Delete) {
    confirmingBulkDelete.value = true
    return
  }
  await runBulkAction(action)
}

/**
 * Deletes the selected tickets once the operator confirmed.
 *
 * @returns Resolves when the delete has settled.
 */
const confirmBulkDelete = async (): Promise<void> => {
  await runBulkAction(TicketBulkAction.Delete)
  confirmingBulkDelete.value = false
}

/** Closes the bulk delete confirmation; ignored while the delete runs. */
const dismissBulkDelete = (): void => {
  if (!bulkRunning.value) {
    confirmingBulkDelete.value = false
  }
}
</script>

<template>
  <UiStack>
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)" >
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="support-tickets-search"
      :search-placeholder="t('support.supportTickets.searchTicketsBySubject')"
      filter-testid="support-tickets-filter"
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
        <SupportTicketFilterFields
          v-model:status="draft.status"
          v-model:priority="draft.priority"
          v-model:department-id="draft.departmentId"
          :departments="store.departments"
          testid-prefix="support-tickets-filter"
        />
        <UiField :label="t('support.supportTickets.flag')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.flagged"
              :options="flagOptions"
              :placeholder="t('shared.any')"
              data-testid="support-tickets-filter-flagged"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>


    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="support-tickets-error"
      @retry="load"
    />

    <UiDataTable
      v-else
      :density="UiTableDensity.Comfortable"
      :loading="initialLoading"
      :empty="!store.loading && store.tickets.length === 0"
      :selection="selection"
      select-all-testid="support-tickets-select-all"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="support-tickets-pagination"
      data-testid="support-tickets-table"
      @update:page="setPage"
    >
        <UiSchemaTableHead :columns="schema.table.columns" />
        <UiTableBody>
          <UiTableRow
            v-for="ticket in store.tickets"
            :key="ticket.id"
            hoverable
            :row-id="ticket.id"
            :select-testid="`support-tickets-select-${ticket.id}`"
            :data-testid="`support-tickets-row-${ticket.id}`"
          >
            <UiTableCell actions>
              <!-- Unflagged: invisible until the row is hovered or the button is focused; flagged: a filled warning flag. -->
              <UiIconButton
                :label="ticket.isFlagged ? t('support.supportTickets.unflag') : t('support.supportTickets.flag')"
                :variant="UiButtonVariant.Ghost"
                :size="UiSize.Sm"
                :class="ticket.isFlagged ? toneTextClasses[UiTone.Warning] : 'text-text-muted opacity-0 focus-visible:opacity-100 [tr:hover_&]:opacity-100'"
                :data-testid="`support-tickets-flag-${ticket.id}`"
                @click="handleToggleFlag(ticket.id)"
              >
                <UiIcon :markup="Icon.Flag" :size="UiSize.Sm" />
              </UiIconButton>
            </UiTableCell>
            <UiTableCell>
              <UiLink
                :to="supportTicketPath(ticket.id)"
                :variant="UiButtonVariant.Link"
                :data-testid="`support-tickets-open-${ticket.id}`"
              >
                {{ t('shared.tktIdSubject', { id: ticket.id, subject: ticket.subject }) }}
              </UiLink>
            </UiTableCell>
            <UiTableCell muted>{{ ticket.departmentName ?? EMPTY_VALUE }}</UiTableCell>
            <UiTableCell muted>{{ ticket.clientName }}</UiTableCell>
            <UiTableCell><SupportTicketPriorityBadge :priority="ticket.priority" /></UiTableCell>
            <UiTableCell><SupportTicketStatusBadge :status="ticket.status" /></UiTableCell>
            <UiTableCell muted>
              {{ ticket.lastReplyAt === null ? t('support.supportTickets.noRepliesYet') : formatRelativeFromNow(ticket.lastReplyAt, locale) }}
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState
            :title="narrowed ? t('support.supportTickets.noTicketsMatchYourFilters') : t('support.supportTickets.noTicketsFound')"
            :description="narrowed ? t('support.supportTickets.tryAdjustingYourFiltersOr') : undefined"
            data-testid="support-tickets-empty"
          >
            <template v-if="activeCount > 0" #actions>
              <UiButton :variant="UiButtonVariant.Link" data-testid="support-tickets-clear-empty" @click="clearAll">
                {{ t('shared.clearFilters') }}
              </UiButton>
            </template>
          </UiEmptyState>
        </template>
    </UiDataTable>

    <UiBulkBar
      :count="selection.count.value"
      :actions="bulkActions"
      data-testid="support-tickets-bulk"
      @clear="selection.clear"
      @action="handleBulkAction($event as TicketBulkAction)"
    />

    <UiDialog
      :open="confirmingBulkDelete"
      :variant="UiDialogVariant.Confirm"
      :title="t('support.supportTickets.confirmBulkDeleteTitle')"
      :message="t('support.supportTickets.confirmBulkDeleteMessage', { count: selection.count.value })"
      :confirm-label="t('shared.delete')"
      :loading="bulkRunning"
      :tone="UiTone.Danger"
      @confirm="confirmBulkDelete"
      @close="dismissBulkDelete"
    />
  </UiStack>
</template>
