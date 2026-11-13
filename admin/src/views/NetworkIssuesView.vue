<script setup lang="ts">
/**
 * Network issues list: a paged list of network issues grouped by status tab, with create, edit
 * and delete actions. A refused load offers a retry; a delete is confirmed in a danger prompt and its
 * outcome (success, or the API's sentence) shows above the table.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatDateTimeCompact } from '@/lib/format'
import { enumValueOf } from '@/lib/enumValue'
import { useNetworkIssuesStore } from '@/stores/networkIssues'
import { NetworkIssueTab } from '@/types/networkissue'
import type { NetworkIssue } from '@/types/networkissue'
import { networkIssuePriorityTone, networkIssueStatusTone } from '@/lib/tones'
import { pageCount } from '@/lib/paging'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiTabItem } from '@/utils/ui/tabs'
import { AppRoute, editPath } from '@/lib/routes'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const store = useNetworkIssuesStore()

/** The tab shown when the page opens. */
const DEFAULT_TAB = NetworkIssueTab.Open

/** One status tab with the heading shown above its list. */
interface StatusTab {
  /** The tab this entry stands for. */
  tab: NetworkIssueTab
  /** Label on the tab. */
  label: string
  /** Heading above the list while the tab is active. */
  heading: string
}

/** Status tabs in display order. */
const statusTabs = computed<StatusTab[]>(() => [
  { tab: NetworkIssueTab.Open, label: t('support.networkIssues.open'), heading: t('support.networkIssues.openIssues') },
  { tab: NetworkIssueTab.Scheduled, label: t('support.networkIssues.scheduled'), heading: t('support.networkIssues.scheduledIssues') },
  { tab: NetworkIssueTab.Resolved, label: t('support.networkIssues.resolved'), heading: t('support.networkIssues.resolvedIssues') },
])

/** Tab items for the kit's tab strip. */
const tabItems = computed<UiTabItem[]>(() =>
  statusTabs.value.map((entry) => ({ value: entry.tab, label: entry.label, testid: `network-issues-tab-${entry.tab}` })),
)

/** Heading above the list for the active tab (the first tab's when the store's filter matches none). */
const activeHeading = computed<string>(
  () => (statusTabs.value.find((entry) => entry.tab === store.statusFilter) ?? statusTabs.value[0])?.heading ?? '',
)

/** ID of the issue waiting for delete confirmation; null when no dialog is open. */
const deleteTarget = ref<number | null>(null)

/** Whether a delete request is in flight. */
const deleting = ref(false)

/** The outcome line of the last delete. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** Number of pages for the record count. */
const totalPages = computed<number>(() => pageCount(store.totalCount, store.pageSize))

/**
 * Switches to a status tab and reloads from the first page.
 *
 * @param value - The tab key the tab strip emitted.
 */
const applyTab = (value: string): void => {
  const tab = enumValueOf(NetworkIssueTab, value)
  if (tab === null) {
    return
  }
  store.statusFilter = tab
  store.page = 1
  store.fetchAll()
}

/**
 * Loads another page.
 *
 * @param page - The page to show, 1-based.
 */
const goToPage = (page: number): void => {
  store.page = page
  store.fetchAll()
}

/**
 * Deletes the issue the dialog asked about, shows the outcome and reloads the list on success.
 *
 * @returns Resolves when the delete (and the reload) has settled.
 */
const handleDelete = async (): Promise<void> => {
  if (deleteTarget.value === null) {
    return
  }
  feedback.clear()
  deleting.value = true
  const removed = await store.remove(deleteTarget.value)
  deleting.value = false
  deleteTarget.value = null
  if (removed) {
    feedback.show(t('shared.recordDeleted'), false)
    await store.fetchAll()
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

onMounted(() => {
  store.statusFilter = DEFAULT_TAB
  store.page = 1
  store.fetchAll()
})

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
    { label: t('shared.edit'), testid: `network-issues-${issue.id}-edit`, to: editPath(AppRoute.SupportNetworkIssues, issue.id) },
    { label: t('shared.delete'), testid: `network-issues-${issue.id}-delete`, danger: true, onSelect: onDelete },
  ]
}
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('shared.networkIssues')"
      :subtitle="t('support.networkIssues.manageNetworkStatusAndIncident')"
    >
      <template #actions>
        <UiLink
          :to="AppRoute.SupportNetworkIssuesNew"
          :variant="UiButtonVariant.Primary"
          data-testid="network-issues-create"
        >
          {{ t('support.networkIssues.createNew') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiTabs :items="tabItems" :model-value="store.statusFilter" @update:model-value="applyTab" />

    <UiHeading :level="2" variant="card" data-testid="network-issues-heading">{{ activeHeading }}</UiHeading>

    <UiStack direction="row" justify="between" wrap>
      <UiText tone="muted" size="sm" data-testid="network-issues-count">
        {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
      </UiText>
      <UiText tone="muted" size="sm">
        {{ t('shared.pagePageOfTotalpages', { page: store.page, totalPages }) }}
      </UiText>
    </UiStack>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="network-issues-feedback" />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="network-issues-error"
      @retry="store.fetchAll()"
    />

    <UiCard v-else>
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="store.loading && store.issues.length === 0"
        :empty="!store.loading && store.issues.length === 0"
        data-testid="network-issues-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.title') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.priority') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('support.networkIssues.startDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('support.networkIssues.endDate') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="issue in store.issues"
            :key="issue.id"
            hoverable
            :data-testid="`network-issues-row-${issue.id}`"
          >
            <UiTableCell>{{ issue.title }}</UiTableCell>
            <UiTableCell muted>{{ issue.type }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="networkIssuePriorityTone[issue.priority]">{{ issue.priority }}</UiBadge>
            </UiTableCell>
            <UiTableCell>
              <UiBadge :tone="networkIssueStatusTone[issue.status]">{{ issue.status }}</UiBadge>
            </UiTableCell>
            <UiTableCell muted>{{ formatDateTimeCompact(issue.startDate) }}</UiTableCell>
            <UiTableCell muted>{{ formatDateTimeCompact(issue.endDate) }}</UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="rowActions(issue)" :testid="`network-issues-${issue.id}-actions`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState
            :title="t('shared.noRecordsFound')"
            :description="t('support.networkIssues.noTolowercaseAtTheMoment', { heading: activeHeading })"
          />
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="store.page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="network-issues-pagination"
      @update:page="goToPage"
    />

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
