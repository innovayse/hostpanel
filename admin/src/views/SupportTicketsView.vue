<script setup lang="ts">
/**
 * Support tickets list: a paged table of all tickets with priority and status badges, a search box,
 * a filter dialog (status or flagged) whose active choice shows as a chip, flagging and bulk
 * actions on the selected rows (`useRowSelection`, the floating `UiBulkBar`). The search, the filter and the page live in the address, so a reload or a shared link
 * shows the same list. A refused load offers a retry; a flag or bulk action shows its outcome
 * (success, or the API's sentence) above the list; a bulk delete is confirmed first in a
 * danger prompt.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import SupportTicketPriorityBadge from '@/components/support/SupportTicketPriorityBadge.vue'
import SupportTicketStatusBadge from '@/components/support/SupportTicketStatusBadge.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { useResultBanner } from '@/composables/useResultBanner'
import { useRowSelection } from '@/composables/useRowSelection'
import { UiDialogVariant } from '@/utils/ui/dialog'
import type { UiFilterChip } from '@/utils/ui/filter'
import { UiTone } from '@/utils/ui/tone'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiBulkBar from '@/components/ui/UiBulkBar.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatElapsed } from '@/lib/format'
import { useSupportStore } from '@/stores/support'
import { TicketBulkAction, TicketFlagFilter, TicketStatus } from '@/types/ticket'
import type { TicketListFilter } from '@/types/ticket'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiSize } from '@/utils/ui/size'
import { toneTextClasses } from '@/utils/ui/tone'
import { AppRoute, supportTicketPath } from '@/lib/routes'

const { t } = useI18n()

const store = useSupportStore()

/** The one filter field of the list, stored in the address under this key. */
const FILTER_KEYS = ['status'] as const

/** One choice of the status filter. */
interface FilterOption {
  /** Value stored in the address; empty shows every active ticket. */
  value: string
  /** The filter the API is asked for; null shows every active ticket. */
  filter: TicketListFilter | null
  /** Translated label. */
  label: string
}

/** Status filter choices in display order. */
const filterOptions = computed<FilterOption[]>(() => [
  { value: '', filter: null, label: t('constants.ticketFilterOptions.allActiveTickets') },
  { value: TicketFlagFilter.Flagged, filter: TicketFlagFilter.Flagged, label: t('constants.ticketFilterOptions.flagged') },
  { value: TicketStatus.Open, filter: TicketStatus.Open, label: t('support.networkIssues.open') },
  { value: TicketStatus.AwaitingReply, filter: TicketStatus.AwaitingReply, label: t('constants.ticketFilterOptions.awaitingReply') },
  { value: TicketStatus.Closed, filter: TicketStatus.Closed, label: t('clients.clientForm.closed') },
])

/** Bulk actions offered for the selected tickets. */
const bulkActions = computed(() => [
  { action: TicketBulkAction.Close, label: t('shared.close'), variant: UiButtonVariant.Primary },
  { action: TicketBulkAction.Delete, label: t('shared.delete'), variant: UiButtonVariant.Danger },
  { action: TicketBulkAction.Flag, label: t('support.supportTickets.flag'), variant: UiButtonVariant.Secondary },
  { action: TicketBulkAction.Unflag, label: t('support.supportTickets.unflag'), variant: UiButtonVariant.Secondary },
])

/** The address-backed filter, search text and page of the list. */
const { applied, draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage } =
  useFilterQuery(FILTER_KEYS)

/** The status choice the address holds; unknown values count as no filter. */
const appliedOption = computed<FilterOption | undefined>(() =>
  filterOptions.value.find((option) => option.value === applied.value.status),
)

/** The filter the list is loaded with. */
const activeFilter = computed<TicketListFilter | null>(() => appliedOption.value?.filter ?? null)

/** Choices of the status select in the filter dialog. */
const statusSelectOptions = computed(() => filterOptions.value.map(({ value, label }) => ({ value, label })))

/** The active filter as a chip, none while every active ticket is shown. */
const chips = computed<UiFilterChip[]>(() => {
  const option = appliedOption.value
  if (option === undefined || option.value === '') {
    return []
  }
  return [{ key: 'status', label: t('shared.status'), value: option.label }]
})

/** The selected tickets of the page. */
const selection = useRowSelection(() => store.tickets.map((ticket) => ticket.id))

/** The outcome line of the last flag or bulk action. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

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

/** First record index (1-based) for display. */
const showingFrom = computed<number>(() => (store.totalCount === 0 ? 0 : (store.page - 1) * store.pageSize + 1))

/** Last record index (1-based) for display. */
const showingTo = computed<number>(() => Math.min(store.page * store.pageSize, store.totalCount))

/**
 * Flips the flag of one ticket and reloads on success.
 *
 * @param id - The ticket identifier.
 */
const handleToggleFlag = async (id: number): Promise<void> => {
  feedback.clear()
  const toggled = await store.toggleFlag(id)
  report(toggled, 'shared.changesSavedSuccessfully')
  if (toggled) {
    await store.fetchAll()
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
  feedback.clear()
  bulkRunning.value = true
  const done = await store.bulkAction([...selection.ids.value], action)
  bulkRunning.value = false
  report(done, action === TicketBulkAction.Delete ? 'shared.recordDeleted' : 'shared.changesSavedSuccessfully')
  if (done) {
    selection.clear()
    await store.fetchAll()
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

/**
 * Loads the list the address asks for: the store is handed the filter, the search text and the page,
 * and the selection (which belonged to the previous page) is dropped.
 *
 * @returns Resolves when the list has settled.
 */
const load = async (): Promise<void> => {
  store.statusFilter = activeFilter.value
  store.search = appliedSearch.value
  store.page = page.value
  selection.clear()
  await store.fetchAll()
}

// The address is the source of truth: the first load, a filter, the search and a page all arrive here.
watch([activeFilter, appliedSearch, page], load, { immediate: true })
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('support.supportTickets.supportTickets')"
      :subtitle="t('support.supportTickets.manageAllClientSupportRequests')"
    >
      <template #actions>
        <UiLink
          :to="AppRoute.SupportTicketsNew"
          :variant="UiButtonVariant.Primary"
          data-testid="support-tickets-open-new"
        >
          {{ t('shared.openNewTicket') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="support-tickets-search"
      :search-placeholder="t('support.supportTickets.searchTicketsBySubject')"
      filter-testid="support-tickets-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove('status')"
      @clear="clearAll"
    >
        <UiField :label="t('shared.status')">
          <UiSelect
            v-model="draft.status"
            :options="statusSelectOptions"
            :placeholder="t('shared.status')"
            data-testid="support-tickets-filter-status"
          />
        </UiField>
    </UiListToolbar>

    <UiText tone="muted" size="sm" data-testid="support-tickets-count">
      {{ t('support.supportTickets.totalcountRecordsFoundShowingShowingfrom', { totalCount: store.totalCount, showingFrom, showingTo }) }}
    </UiText>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="support-tickets-feedback" />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="support-tickets-error"
      @retry="store.fetchAll()"
    />

    <UiCard v-else>
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="store.loading && store.tickets.length === 0"
        :empty="!store.loading && store.tickets.length === 0"
        :selection="selection"
        select-all-testid="support-tickets-select-all"
        data-testid="support-tickets-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader />
            <UiTableHeader>{{ t('shared.subject') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.department') }}</UiTableHeader>
            <UiTableHeader>{{ t('support.supportTickets.requestor') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.priority') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.lastReply') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="ticket in store.tickets"
            :key="ticket.id"
            hoverable
            :row-id="ticket.id"
            :select-testid="`support-tickets-select-${ticket.id}`"
            :data-testid="`support-tickets-row-${ticket.id}`"
          >
            <UiTableCell>
              <UiIconButton
                :label="t('support.supportTickets.flag')"
                :size="UiSize.Sm"
                :class="ticket.isFlagged ? toneTextClasses[UiTone.Warning] : 'text-text-muted'"
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
            <UiTableCell muted>{{ ticket.departmentName ?? '—' }}</UiTableCell>
            <UiTableCell muted>{{ ticket.clientName }}</UiTableCell>
            <UiTableCell><SupportTicketPriorityBadge :priority="ticket.priority" /></UiTableCell>
            <UiTableCell><SupportTicketStatusBadge :status="ticket.status" /></UiTableCell>
            <UiTableCell muted>{{ formatElapsed(ticket.lastReplyAt) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState
            :title="t('support.supportTickets.noTicketsFound')"
            :description="t('support.supportTickets.tryAdjustingYourFiltersOr')"
          />
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="store.page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="support-tickets-pagination"
      @update:page="setPage"
    />

    <UiBulkBar :count="selection.count.value" data-testid="support-tickets-bulk" @clear="selection.clear">
      <UiButton
        v-for="entry in bulkActions"
        :key="entry.action"
        :variant="entry.variant"
        :size="UiSize.Sm"
        :disabled="bulkRunning"
        :data-testid="`support-tickets-bulk-${entry.action}`"
        @click="handleBulkAction(entry.action)"
      >
        {{ entry.label }}
      </UiButton>
    </UiBulkBar>

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
