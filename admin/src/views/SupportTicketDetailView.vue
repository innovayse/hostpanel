<script setup lang="ts">
/**
 * Support ticket detail: the conversation as chat bubbles (the client on the left, staff on the right)
 * with the reply composer pinned to the bottom of the page, and a side card with the ticket's details
 * and its department and priority controls. Below the `lg` breakpoint the side card moves into a bottom
 * sheet opened from the header. Close, reopen and delete sit in the header. A refused load offers a
 * retry; each action shows its outcome (success, or the API's sentence) next to its own controls;
 * deleting is confirmed in a danger prompt and returns to the list.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useFallbackLabels } from '@/composables/useFallbackLabels'
import SupportTicketMessage from '@/components/support/SupportTicketMessage.vue'
import SupportTicketMeta from '@/components/support/SupportTicketMeta.vue'
import SupportTicketPriorityBadge from '@/components/support/SupportTicketPriorityBadge.vue'
import SupportTicketStatusBadge from '@/components/support/SupportTicketStatusBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiChatBubble, { UiChatBubbleSide } from '@/components/ui/UiChatBubble.vue'
import UiChatInput from '@/components/ui/UiChatInput.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { usePageCrumbs } from '@/composables/usePageCrumbs'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { useSupportStore } from '@/stores/support'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import type { UpdateTicketPayload } from '@/types/ticket'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { AppRoute } from '@/lib/routes'
import schema, { COMPOSER_CARD, CONVERSATION_CARD, DETAILS_CARD } from '@/views/SupportTicketDetailView.schema'
import { UiActionKind } from '@/utils/ui/actions'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** Builds the trail the header draws above the title: Support, Tickets, the ticket. */
const { crumbs } = usePageCrumbs()

/** Opens the delete confirmation. */
const openDeleteDialog = (): void => {
  showDeleteDialog.value = true
}

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  ...(store.currentTicket?.status !== TicketStatus.Closed
    ? [headerAction(UiActionKind.Close, { label: t('shared.close'), variant: UiButtonVariant.Secondary, testid: 'ticket-close', disabled: !store.currentTicket, onSelect: handleClose })]
    : [headerAction(UiActionKind.Refresh, { label: t('support.supportTicketDetail.reopen'), variant: UiButtonVariant.Secondary, testid: 'ticket-reopen', onSelect: handleReopen })]),
  headerAction(UiActionKind.Delete, { label: t('shared.delete'), variant: UiButtonVariant.Danger, testid: 'ticket-delete', disabled: !store.currentTicket, onSelect: openDeleteDialog }),
])


/** Fallback text for fields the API leaves unresolved. */

const { orUnknown } = useFallbackLabels()

const route = useRoute()
const router = useRouter()
const store = useSupportStore()

/** Ticket ID from route params. */
const ticketId = computed<string>(() => String(route.params.id))

/** Editable department ID for the admin controls (0 = none chosen). */
const editDepartmentId = ref<number>(0)

/** Editable priority for the admin controls. */
const editPriority = ref<TicketPriority>(TicketPriority.Medium)

/** Reply message text. */
const replyMessage = ref('')

/** Whether the delete confirmation dialog is visible. */
const showDeleteDialog = ref(false)

/** Reports the outcome of every action of the screen (close, reopen, delete, save, reply) as a toast. */
const outcome = useOutcomeToast()

/**
 * Reports the outcome of a change: the success sentence, or the API's refusal.
 *
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 */
const report = (ok: boolean, successKey: string): void => {
  if (ok) {
    outcome.show(t(successKey), false)
  } else {
    outcome.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/** Whether the details sheet is open. */
const detailsOpen = ref(false)

/** Department options mapped for UiSelect. */
const departmentOptions = computed(() => store.departments.map((department) => ({ value: department.id, label: department.name })))

/** Whether the reply composer can be sent. */
const canReply = computed<boolean>(() => replyMessage.value.trim() !== '')

/** Last reply timestamp from the ticket's replies, null when there is none. */
const lastReplyAt = computed<string | null>(() => {
  const replies = store.currentTicket?.replies ?? []
  return replies[replies.length - 1]?.createdAt ?? null
})

/**
 * Copies the loaded ticket's department and priority into the editable fields.
 */
const populateForm = (): void => {
  const ticket = store.currentTicket
  if (!ticket) {
    return
  }
  editDepartmentId.value = ticket.department.id ?? 0
  editPriority.value = ticket.priority
}

/**
 * Reloads the ticket after a change went through.
 *
 * @param accepted - Whether the change was accepted.
 */
const reloadIf = async (accepted: boolean): Promise<void> => {
  if (accepted) {
    await store.fetchById(ticketId.value)
  }
}

/**
 * Saves the department and priority chosen in the admin controls.
 */
const handleSaveChanges = async (): Promise<void> => {
  const payload: UpdateTicketPayload = { priority: editPriority.value }
  if (editDepartmentId.value) {
    payload.departmentId = editDepartmentId.value
  }
  const saved = await store.updateTicket(ticketId.value, payload)
  report(saved, 'shared.changesSavedSuccessfully')
  await reloadIf(saved)
}

/**
 * Closes the ticket.
 */
const handleClose = async (): Promise<void> => {
  const closed = await store.updateTicket(ticketId.value, { status: TicketStatus.Closed })
  report(closed, 'support.supportTicketDetail.ticketClosed')
  await reloadIf(closed)
}

/**
 * Reopens a closed ticket.
 */
const handleReopen = async (): Promise<void> => {
  const reopened = await store.updateTicket(ticketId.value, { status: TicketStatus.Open })
  report(reopened, 'support.supportTicketDetail.ticketReopened')
  await reloadIf(reopened)
}

/**
 * Sends the staff reply and clears the message on success.
 */
const handleReply = async (): Promise<void> => {
  if (!canReply.value) {
    return
  }
  const sent = await store.replyToTicket(ticketId.value, replyMessage.value.trim())
  report(sent, 'support.supportTicketDetail.replySent')
  if (sent) {
    replyMessage.value = ''
    await store.fetchById(ticketId.value)
  }
}

/**
 * Deletes the ticket and returns to the tickets list.
 */
const handleDelete = async (): Promise<void> => {
  const deleted = await store.deleteTicket(ticketId.value)
  showDeleteDialog.value = false
  if (deleted) {
    router.push(AppRoute.SupportTickets)
  } else {
    outcome.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!store.loading) {
    showDeleteDialog.value = false
  }
}

/** Loads the ticket of the route again, after a refused load. */
const reload = (): void => {
  void store.fetchById(ticketId.value)
}

// The editable fields follow whichever ticket is loaded.
watch(() => store.currentTicket, populateForm)

onMounted(() => {
  store.fetchById(ticketId.value)
  store.fetchDepartments()
})
</script>

<template>
  <UiStack>
    <UiErrorState
      v-if="store.error && !store.currentTicket"
      :message="store.error"
      :loading="store.loading"
      testid="ticket-error"
      @retry="reload"
    />

    <template v-else>
      <UiPageHeader :actions="headerActions"
        :title="store.currentTicket ? t('shared.tktIdSubject', { id: store.currentTicket.id, subject: store.currentTicket.subject }) : undefined"
        :loading="!store.currentTicket"
        :crumbs="crumbs(store.currentTicket ? t('shared.tktIdSubject', { id: store.currentTicket.id, subject: store.currentTicket.subject }) : undefined)"
        data-testid="ticket-title"
      >
        <template #badge>
          <UiStack v-if="store.currentTicket" :direction="UiDirection.Row" :align="UiAlignItems.Center" wrap>
            <SupportTicketStatusBadge :status="store.currentTicket.status" />
            <SupportTicketPriorityBadge :priority="store.currentTicket.priority" />
          </UiStack>
        </template>
        <template #actions>
          <UiButton :icon="Icon.Info"
            :variant="UiButtonVariant.Secondary"
            class="lg:hidden"
            :disabled="!store.currentTicket"
            data-testid="ticket-details-open"
            @click="detailsOpen = true"
          >{{ t('shared.details') }}
          </UiButton>
        </template>
      </UiPageHeader>


      <UiSplit v-model:open="detailsOpen" :ratio="schema.ratio" :aside-title="t(DETAILS_CARD.title)">
        <UiStack>
          <UiCard :padding="UiCardPadding.Md" data-testid="ticket-thread">
            <UiStack>
              <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ t(CONVERSATION_CARD.title) }}</UiHeading>

              <template v-if="store.currentTicket">
                <SupportTicketMessage
                  :author="t('shared.client')"
                  :created-at="store.currentTicket.createdAt"
                  :staff="false"
                  data-testid="ticket-initial-message"
                >
                  {{ store.currentTicket.message }}
                </SupportTicketMessage>

                <SupportTicketMessage
                  v-for="reply in store.currentTicket.replies"
                  :key="reply.id"
                  :author="orUnknown(reply.authorName)"
                  :created-at="reply.createdAt"
                  :staff="reply.isStaffReply"
                  data-testid="ticket-reply"
                >
                  {{ reply.message }}
                </SupportTicketMessage>

                <UiEmptyState
                  v-if="store.currentTicket.replies.length === 0"
                  :title="t('support.supportTicketDetail.noRepliesYet')"
                  data-testid="ticket-no-replies"
                />
              </template>
              <template v-else>
                <UiChatBubble :side="UiChatBubbleSide.Other" loading data-testid="ticket-message-loading" />
                <UiChatBubble :side="UiChatBubbleSide.Own" loading data-testid="ticket-message-loading" />
                <UiChatBubble :side="UiChatBubbleSide.Other" loading data-testid="ticket-message-loading" />
              </template>
            </UiStack>
          </UiCard>

          <!-- Pinned to the bottom of the scrolling page, so the reply field is always in reach. -->
          <UiCard :padding="UiCardPadding.Sm" elevated class="sticky bottom-0 z-10" data-testid="ticket-composer">
            <UiStack :gap="UiGap.Sm">
              <UiChatInput
                v-model="replyMessage"
                :label="t(COMPOSER_CARD.rows[0])"
                :placeholder="t('support.supportTicketDetail.typeYourReply')"
                :disabled="!store.currentTicket"
                testid="ticket-reply-message"
                @submit="handleReply"
              >
                <template #action>
                  <UiIconButton
                    :label="t('shared.reply')"
                    :variant="UiButtonVariant.Primary"
                    :size="UiSize.Sm"
                    :disabled="store.loading || !canReply || !store.currentTicket"
                    data-testid="ticket-reply-submit"
                    @click="handleReply"
                  >
                    <UiIcon :markup="Icon.ArrowUp" :size="UiSize.Sm" />
                  </UiIconButton>
                </template>
              </UiChatInput>
            </UiStack>
          </UiCard>
        </UiStack>

        <template #aside>
          <SupportTicketMeta
            v-model:department-id="editDepartmentId"
            v-model:priority="editPriority"
            :ticket="store.currentTicket"
            :department-options="departmentOptions"
            :departments-error="store.departmentsError"
            :last-reply-at="lastReplyAt"
            :saving="store.loading"
            @save="handleSaveChanges"
            @retry-departments="store.fetchDepartments"
          />
        </template>
      </UiSplit>
    </template>

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="showDeleteDialog"
      :title="t('support.supportTicketDetail.deleteTicket')"
      :message="t('support.supportTicketDetail.deleteTicketMessage', { id: ticketId })"
      :confirm-label="t('shared.delete')"
      :loading="store.loading"
      :tone="UiTone.Danger"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
