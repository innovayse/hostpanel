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
import UiLink from '@/components/ui/UiLink.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import type { ResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiSplitRatio } from '@/utils/ui/layout'
import { useSupportStore } from '@/stores/support'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import type { UpdateTicketPayload } from '@/types/ticket'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()


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

/** Outcome line of the header actions (close, reopen, delete). */
const headerFeedback = useResultBanner()

/** Outcome line of the department and priority save. */
const saveFeedback = useResultBanner()

/** Outcome line of the staff reply. */
const replyFeedback = useResultBanner()

/**
 * Tone of an outcome line: danger for a refusal, success otherwise.
 *
 * @param banner - The outcome line.
 * @returns The tone to draw it in.
 */
const toneOf = (banner: ResultBanner): UiTone => (banner.isError.value ? UiTone.Danger : UiTone.Success)

/**
 * Shows the outcome of a change in its own line: the success sentence, or the API's refusal.
 *
 * @param banner - The outcome line next to the action.
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 */
const report = (banner: ResultBanner, ok: boolean, successKey: string): void => {
  if (ok) {
    banner.show(t(successKey), false)
  } else {
    banner.show(store.actionError ?? t('errors.noAnswer'), true)
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
  saveFeedback.clear()
  const saved = await store.updateTicket(ticketId.value, payload)
  report(saveFeedback, saved, 'shared.changesSavedSuccessfully')
  await reloadIf(saved)
}

/**
 * Closes the ticket.
 */
const handleClose = async (): Promise<void> => {
  headerFeedback.clear()
  const closed = await store.updateTicket(ticketId.value, { status: TicketStatus.Closed })
  report(headerFeedback, closed, 'support.supportTicketDetail.ticketClosed')
  await reloadIf(closed)
}

/**
 * Reopens a closed ticket.
 */
const handleReopen = async (): Promise<void> => {
  headerFeedback.clear()
  const reopened = await store.updateTicket(ticketId.value, { status: TicketStatus.Open })
  report(headerFeedback, reopened, 'support.supportTicketDetail.ticketReopened')
  await reloadIf(reopened)
}

/**
 * Sends the staff reply and clears the message on success.
 */
const handleReply = async (): Promise<void> => {
  if (!canReply.value) {
    return
  }
  replyFeedback.clear()
  const sent = await store.replyToTicket(ticketId.value, replyMessage.value.trim())
  report(replyFeedback, sent, 'support.supportTicketDetail.replySent')
  if (sent) {
    replyMessage.value = ''
    await store.fetchById(ticketId.value)
  }
}

/**
 * Deletes the ticket and returns to the tickets list.
 */
const handleDelete = async (): Promise<void> => {
  headerFeedback.clear()
  const deleted = await store.deleteTicket(ticketId.value)
  showDeleteDialog.value = false
  if (deleted) {
    router.push(AppRoute.SupportTickets)
  } else {
    headerFeedback.show(store.actionError ?? t('errors.noAnswer'), true)
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
      <UiStack direction="row" justify="between" align="center" wrap :aria-busy="store.currentTicket ? undefined : 'true'">
        <UiStack direction="row" align="center" wrap>
          <UiLink
            :to="AppRoute.SupportTickets"
            :variant="UiButtonVariant.Secondary"
            :size="UiSize.Sm"
            data-testid="ticket-back"
          >
            <UiIcon :markup="Icon.Back" :size="UiSize.Sm" />
            {{ t('shared.back') }}
          </UiLink>
          <template v-if="store.currentTicket">
            <UiHeading :level="1" variant="card" data-testid="ticket-title">
              {{ t('shared.tktIdSubject', { id: store.currentTicket.id, subject: store.currentTicket.subject }) }}
            </UiHeading>
            <SupportTicketStatusBadge :status="store.currentTicket.status" />
            <SupportTicketPriorityBadge :priority="store.currentTicket.priority" />
          </template>
          <UiStack v-else direction="row" align="center" wrap aria-hidden="true" data-testid="ticket-loading">
            <UiSkeletonBar class="h-6 w-64 max-w-full rounded-md" />
            <UiSkeletonBar class="h-5 w-20 rounded-full" />
            <UiSkeletonBar class="h-5 w-16 rounded-full" />
          </UiStack>
        </UiStack>
        <UiStack direction="row" gap="sm" wrap>
          <UiButton
            :variant="UiButtonVariant.Secondary"
            class="lg:hidden"
            :disabled="!store.currentTicket"
            data-testid="ticket-details-open"
            @click="detailsOpen = true"
          >
            <UiIcon :markup="Icon.Clipboard" :size="UiSize.Sm" />
            {{ t('support.supportTicketDetail.details') }}
          </UiButton>
          <UiButton
            v-if="store.currentTicket?.status !== TicketStatus.Closed"
            :variant="UiButtonVariant.Secondary"
            :disabled="!store.currentTicket"
            data-testid="ticket-close"
            @click="handleClose"
          >
            {{ t('shared.close') }}
          </UiButton>
          <UiButton
            v-else
            :variant="UiButtonVariant.Secondary"
            data-testid="ticket-reopen"
            @click="handleReopen"
          >
            {{ t('support.supportTicketDetail.reopen') }}
          </UiButton>
          <UiButton :variant="UiButtonVariant.Danger" :disabled="!store.currentTicket" data-testid="ticket-delete" @click="showDeleteDialog = true">
            {{ t('shared.delete') }}
          </UiButton>
        </UiStack>
      </UiStack>

      <UiStatusMessage :message="headerFeedback.message.value" :tone="toneOf(headerFeedback)" data-testid="ticket-action-feedback" />

      <UiSplit v-model:open="detailsOpen" :ratio="UiSplitRatio.ThreeQuarters" :aside-title="t('support.supportTicketDetail.ticketSettings')">
        <UiStack>
          <UiCard padding="md" data-testid="ticket-thread">
            <UiStack>
              <UiHeading :level="2" variant="card">{{ t('support.supportTicketDetail.conversation') }}</UiHeading>

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
          <UiCard padding="sm" elevated class="sticky bottom-0 z-10" data-testid="ticket-composer">
            <UiStack gap="sm">
              <UiChatInput
                v-model="replyMessage"
                :label="t('shared.addReply')"
                :placeholder="t('support.supportTicketDetail.typeYourReply')"
                :disabled="!store.currentTicket"
                testid="ticket-reply-message"
                @submit="handleReply"
              >
                <template #action>
                  <UiIconButton
                    :label="t('support.supportTicketDetail.reply')"
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
              <UiStatusMessage :message="replyFeedback.message.value" :tone="toneOf(replyFeedback)" data-testid="ticket-reply-feedback" />
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
            :feedback="saveFeedback.message.value"
            :feedback-tone="toneOf(saveFeedback)"
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
