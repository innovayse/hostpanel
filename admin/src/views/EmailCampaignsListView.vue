<script setup lang="ts">
/**
 * Email campaigns list: a search box, the filter dialog with its removable chips, and a paged table of the campaigns
 * with their status, send times and delivery counters, a button to write a new one, and open and delete actions (a
 * campaign can only be deleted while it is a draft).
 *
 * The subject search, the status filter and the page live in the address (`useListQuery`), so a reload, a shared link
 * and the back button restore the same list, and the API applies them before it pages. Deleting is confirmed first,
 * its outcome shows above the table and the page the address names is read again; a refused load shows the API
 * sentence with a Retry.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
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
import schema from '@/views/EmailCampaignsListView.schema'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { CAMPAIGN_STATUS_OPTIONS } from '@/lib/constants'
import { formatDateTimeCompact } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { AppRoute, emailCampaignPath } from '@/lib/routes'
import { campaignStatusTone, statusLabel } from '@/lib/tones'
import { schemaHeader } from '@/lib/schemaHeader'
import { useEmailCampaignsStore } from '@/stores/emailCampaigns'
import { CampaignStatus } from '@/types/emailcampaign'
import type { EmailCampaignListFilters, EmailCampaignListItem } from '@/types/emailcampaign'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import type { UiHeaderAction } from '@/utils/ui/actionBar'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('emailCampaigns.list.create'), icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: 'email-campaigns-add', to: AppRoute.SupportEmailCampaignsNew },
])

/** The filter field the dialog edits; it is one query key and one `EmailCampaignListFilters` member. */
const FilterKey = {
  Status: 'status',
} as const

/** Campaigns store: the page, the loading flag and the refusal sentence. */
const store = useEmailCampaignsStore()

/** The status filter, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, stepBackWhenEmpty, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Status filter options; the empty value means any. */
const statusOptions = computed(() => [{ value: '', label: t('shared.any') }, ...CAMPAIGN_STATUS_OPTIONS])

/** The status in the address as a campaign status, undefined when none is set or the value is not one. */
const statusOfAddress = enumOf(FilterKey.Status, CampaignStatus)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<EmailCampaignListFilters>(() => ({
  search: appliedSearch.value,
  status: statusOfAddress.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Status,
    label: t('shared.status'),
    value: statusOfAddress.value === undefined ? undefined : statusLabel(CAMPAIGN_STATUS_OPTIONS, statusOfAddress.value),
  },
])

/** ID of the campaign waiting for delete confirmation; null when no dialog is open. */
const deleteTarget = ref<number | null>(null)

/** Whether a delete request is in flight. */
const deleting = ref(false)

/** The outcome line of the last delete. */
const feedback = useOutcomeToast()

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. The store keeps one sentence for every call, and a refused delete must not hide the list. */
const loadError = ref<string | null>(null)

/**
 * Loads the page the address names with the filter and search it holds, and records whether the load was refused.
 *
 * @returns True when loaded; false when refused (see `loadError`).
 */
const load = async (): Promise<boolean> => {
  const loaded = await store.fetchAll(page.value, filters.value)
  loadError.value = loaded ? null : store.error
  return loaded
}

// Every change of the status, the search or the page loads the list the address names.
loadOnChange(load)

/** Whether the current page has any campaign to list. */
const hasRows = computed<boolean>(() => store.campaigns.length > 0)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && !hasRows.value)

/**
 * Deletes the campaign the dialog asked about, shows the outcome and reloads the page on success. When the delete
 * emptied the last page, the list steps back to the page before it.
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
    feedback.show(t('emailCampaigns.list.deleted'), false)
    await load()
    await stepBackWhenEmpty(!hasRows.value)
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
 * The row actions of a campaign: open it (edit while a draft, read-only after), and delete a draft last.
 *
 * @param campaign - The campaign of the row.
 * @returns The action list passed to UiRowActions.
 */
const rowActions = (campaign: EmailCampaignListItem): UiMenuAction[] => {
  const isDraft = campaign.status === CampaignStatus.Draft
  const actions: UiMenuAction[] = [
    {
      label: isDraft ? t('shared.edit') : t('shared.view'),
      icon: isDraft ? Icon.Edit : Icon.Eye,
      testid: `email-campaigns-${campaign.id}-open`,
      to: emailCampaignPath(campaign.id),
    },
  ]
  if (isDraft) {
    /**
     * Stages this campaign for the delete confirmation.
     *
     * @returns Nothing.
     */
    const onDelete = (): void => {
      deleteTarget.value = campaign.id
    }
    actions.push({ label: t('shared.delete'), icon: Icon.Trash, testid: `email-campaigns-${campaign.id}-delete`, danger: true, onSelect: onDelete })
  }
  return actions
}
</script>

<template>
  <UiStack>
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>


    <UiListToolbar
      v-model:search="search"
      search-testid="email-campaigns-search"
      :search-placeholder="t('emailCampaigns.list.searchPlaceholder')"
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
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.status"
              :options="statusOptions"
              :placeholder="t('shared.any')"
              data-testid="email-campaigns-filter-status"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="email-campaigns-error"
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
      pagination-testid="email-campaigns-pagination"
      data-testid="email-campaigns-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="campaign in store.campaigns"
          :key="campaign.id"
          hoverable
          :data-testid="`email-campaigns-row-${campaign.id}`"
        >
          <UiTableCell>{{ campaign.subject }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="campaignStatusTone[campaign.status]">
              {{ t(`emailCampaigns.status.${campaign.status}`) }}
            </UiBadge>
          </UiTableCell>
          <UiTableCell muted>{{ formatDateTimeCompact(campaign.scheduledAt) }}</UiTableCell>
          <UiTableCell muted>{{ formatDateTimeCompact(campaign.sentAt) }}</UiTableCell>
          <UiTableCell muted>
            {{ t('emailCampaigns.list.deliverySummary', {
              sent: campaign.stats.sent,
              failed: campaign.stats.failed,
              unsubscribed: campaign.stats.unsubscribed,
            }) }}
          </UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="rowActions(campaign)" :testid="`email-campaigns-${campaign.id}-actions`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState
          :title="narrowed ? t('emailCampaigns.list.noMatches') : t('emailCampaigns.list.emptyTitle')"
          :description="narrowed ? undefined : t('emailCampaigns.list.emptyDescription')"
          data-testid="email-campaigns-empty"
        >
          <template #icon>
            <UiIcon :markup="Icon.Mail" size="lg" />
          </template>
          <template v-if="activeCount > 0" #actions>
            <UiButton :variant="UiButtonVariant.Link" data-testid="email-campaigns-clear-empty" @click="clearAll">
              {{ t('shared.clearFilters') }}
            </UiButton>
          </template>
        </UiEmptyState>
      </template>
    </UiDataTable>

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="deleteTarget !== null"
      :title="t('emailCampaigns.list.deleteTitle')"
      :message="t('emailCampaigns.list.deleteMessage')"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
