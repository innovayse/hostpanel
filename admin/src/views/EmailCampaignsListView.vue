<script setup lang="ts">
/**
 * Email campaigns list: every campaign with its status, send times and delivery counters, a
 * button to write a new one, and open and delete actions (a campaign can only be deleted while
 * it is a draft). Deleting is confirmed first and its outcome shows above the table; a refused
 * load shows the API's sentence with a Retry.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
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
import { useResultBanner } from '@/composables/useResultBanner'
import { formatDateTimeCompact } from '@/lib/format'
import { AppRoute, emailCampaignPath } from '@/lib/routes'
import { campaignStatusTone } from '@/lib/tones'
import { useEmailCampaignsStore } from '@/stores/emailCampaigns'
import { CampaignStatus } from '@/types/emailcampaign'
import type { EmailCampaignListItem } from '@/types/emailcampaign'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const store = useEmailCampaignsStore()

/** ID of the campaign waiting for delete confirmation; null when no dialog is open. */
const deleteTarget = ref<number | null>(null)

/** Whether a delete request is in flight. */
const deleting = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last delete. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Loads the list and records whether the load was refused.
 *
 * @returns Resolves when the list has settled.
 */
const load = async (): Promise<void> => {
  loadError.value = (await store.fetchAll()) ? null : store.error
}

/**
 * Deletes the campaign the dialog asked about, shows the outcome and reloads the list on success.
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
    feedback.show(t('emailCampaigns.list.deleted'), false)
    await load()
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

/** Loads the list. */
onMounted(() => {
  void load()
})

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
    actions.push({ label: t('shared.delete'), testid: `email-campaigns-${campaign.id}-delete`, danger: true, onSelect: onDelete })
  }
  return actions
}
</script>

<template>
  <UiStack>
    <UiPageHeader :title="t('emailCampaigns.list.title')" :subtitle="t('emailCampaigns.list.subtitle')">
      <template #actions>
        <UiLink
          :to="AppRoute.SupportEmailCampaignsNew"
          :variant="UiButtonVariant.Primary"
          data-testid="email-campaigns-add"
        >
          {{ t('emailCampaigns.list.add') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="email-campaigns-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="email-campaigns-error"
      @retry="load"
    />

    <UiCard class="overflow-x-auto">
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="store.loading && store.campaigns.length === 0"
        :empty="!store.loading && store.campaigns.length === 0"
        data-testid="email-campaigns-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.subject') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('emailCampaigns.list.scheduledAt') }}</UiTableHeader>
            <UiTableHeader>{{ t('emailCampaigns.list.sentAt') }}</UiTableHeader>
            <UiTableHeader>{{ t('emailCampaigns.list.delivery') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
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
            :title="t('emailCampaigns.list.emptyTitle')"
            :description="t('emailCampaigns.list.emptyDescription')"
          />
        </template>
      </UiTable>
    </UiCard>

    <UiDialog
      variant="confirm"
      :open="deleteTarget !== null"
      :title="t('emailCampaigns.list.deleteTitle')"
      :message="t('emailCampaigns.list.deleteMessage')"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      tone="danger"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
