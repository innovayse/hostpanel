<script setup lang="ts">
/**
 * Client ticket detail page: displays full ticket data with admin controls, reply form, and
 * conversation thread. Supports status changes, priority/department updates, and ticket deletion.
 * Deleting is confirmed first and leaves for the tickets list; every other action reports its
 * outcome (success, or the API's sentence) under the header, and a refused load shows the
 * sentence with a Retry; a refused department lookup shows its sentence with a Retry in place of
 * the department selector.
 */
import { ref, computed, onMounted, provide, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useFallbackLabels } from '@/composables/useFallbackLabels'
import { useResultBanner } from '@/composables/useResultBanner'
import { useClientTicketsStore } from '@/stores/clientTickets'
import { formatDate, formatDateTimeCompact } from '@/lib/format'
import { TICKET_PRIORITY_OPTIONS } from '@/lib/constants'
import { ticketStatusLabel, ticketStatusTone } from '@/lib/tones'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiChatBubble, { UiChatBubbleSide } from '@/components/ui/UiChatBubble.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { ClientSection, clientSectionPath } from '@/lib/routes'

const { t } = useI18n()


/** Fallback text for fields the API leaves unresolved. */

const { orUnknown } = useFallbackLabels()

/** Icon markup (shape tags) of the back link. */
const BACK_ICON = '<polyline points="15 18 9 12 15 6"/>'

/** Current route, source of the client and ticket ids. */
const route = useRoute()

/** Router, used to leave after a delete. */
const router = useRouter()

/** Client ticket store, the only caller of the ticket endpoints. */
const store = useClientTicketsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Ticket ID from route params. */
const ticketId = computed(() => route.params.ticketId as string)

/** Editable department ID for admin controls. */
const editDepartmentId = ref<number>(0)

/** Editable priority for admin controls. */
const editPriority = ref<TicketPriority>(TicketPriority.Medium)

/** Reply message text. */
const replyMessage = ref('')

/** Whether the delete confirmation modal is visible. */
const showDeleteModal = ref(false)

/** Department options mapped for UiSelect. */
const departmentOptions = computed(() =>
  store.departments.map(d => ({ value: d.id, label: d.name })),
)

/** Priority options for UiSelect. */
const priorityOptions = TICKET_PRIORITY_OPTIONS

/** True when the reply form cannot be sent yet. */
const replyIncomplete = computed<boolean>(() => !replyMessage.value.trim())

/** Last reply timestamp from the ticket's replies. */
const lastReplyAt = computed(() => {
  const ticket = store.currentTicket
  if (!ticket || ticket.replies.length === 0) {
    return null
  }
  // The length check above guarantees a last reply; `?.` only narrows the index access.
  return ticket.replies[ticket.replies.length - 1]?.createdAt ?? null
})

/**
 * Populates editable fields from the loaded ticket.
 *
 * @returns Nothing.
 */
const populateForm = (): void => {
  const ticket = store.currentTicket
  if (!ticket) {
    return
  }
  editDepartmentId.value = ticket.department.id ?? 0
  editPriority.value = ticket.priority
}

/** The outcome line of the last change. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** True while the delete is in flight. */
const deleting = ref(false)

/**
 * Loads the ticket and the departments.
 *
 * @returns Promise that resolves when both have settled.
 */
const load = async (): Promise<void> => {
  await Promise.all([store.fetchById(ticketId.value), store.fetchDepartments()])
}

/**
 * Runs one change of the ticket, shows its outcome and reloads the ticket when it was accepted.
 *
 * @param change - The store call to run.
 * @param successKey - Locale key of the success sentence.
 * @returns True when the change was accepted.
 */
const runChange = async (change: () => Promise<boolean>, successKey: string): Promise<boolean> => {
  feedback.clear()
  const ok = await change()
  if (ok) {
    feedback.show(t(successKey), false)
    await store.fetchById(ticketId.value)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
  return ok
}

/**
 * Saves admin control changes (department, priority).
 *
 * @returns Promise that resolves when update completes.
 */
const handleSaveChanges = async (): Promise<void> => {
  const payload: { departmentId?: number; priority?: TicketPriority } = {}
  if (editDepartmentId.value) {
    payload.departmentId = editDepartmentId.value
  }
  payload.priority = editPriority.value

  await runChange(() => store.updateTicket(ticketId.value, payload), 'clients.clientTicketDetail.ticketSaved')
}

/**
 * Closes an open ticket by setting status to Closed.
 *
 * @returns Promise that resolves when update completes.
 */
const handleClose = async (): Promise<void> => {
  await runChange(() => store.updateTicket(ticketId.value, { status: TicketStatus.Closed }), 'clients.clientTicketDetail.ticketClosed')
}

/**
 * Reopens a closed ticket by setting status to Open.
 *
 * @returns Promise that resolves when update completes.
 */
const handleReopen = async (): Promise<void> => {
  await runChange(() => store.updateTicket(ticketId.value, { status: TicketStatus.Open }), 'clients.clientTicketDetail.ticketReopened')
}

/**
 * Submits a staff reply to the ticket.
 *
 * @returns Promise that resolves when reply is sent.
 */
const handleReply = async (): Promise<void> => {
  if (replyIncomplete.value) {
    return
  }

  const message = replyMessage.value.trim()
  if (await runChange(() => store.replyToTicket(ticketId.value, message), 'clients.clientTicketDetail.replySent')) {
    replyMessage.value = ''
  }
}

/**
 * Deletes the ticket and navigates back to the tickets list; a refusal shows the API's sentence
 * under the header.
 *
 * @returns Promise that resolves when deletion completes.
 */
const handleDelete = async (): Promise<void> => {
  feedback.clear()
  deleting.value = true
  const ok = await store.deleteTicket(ticketId.value)
  deleting.value = false
  showDeleteModal.value = false
  if (ok) {
    await router.push(clientSectionPath(clientId.value, ClientSection.Tickets))
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    showDeleteModal.value = false
  }
}

/** True while the ticket is read for the first time: the page keeps its real layout and draws it as placeholders. */
const initialLoading = computed<boolean>(() => store.currentTicket === null && store.error === null)

// Every field of the page stays a placeholder until the ticket has arrived.
provide(UI_FIELD_LOADING, initialLoading)

// Populate form when ticket loads
watch(() => store.currentTicket, () => {
  populateForm()
})

/** Loads the ticket and the departments. */
onMounted(() => {
  void load()
})
</script>

<template>
  <UiStack>
    <UiErrorState
      v-if="store.error && !store.currentTicket"
      :message="store.error"
      :loading="store.loading"
      testid="client-ticket-error"
      @retry="load"
    />

    <template v-else>
      <UiStack direction="row" wrap :justify="UiJustify.Between" :align="UiAlignItems.Center" :aria-busy="store.currentTicket ? undefined : 'true'">
        <UiStack direction="row" :align="UiAlignItems.Center">
          <UiLink
            :to="clientSectionPath(clientId, ClientSection.Tickets)"
            :variant="UiButtonVariant.Secondary"
            data-testid="client-ticket-back"
          >
            <UiIcon :markup="BACK_ICON" :size="UiSize.Sm" />
            {{ t('shared.back') }}
          </UiLink>
          <template v-if="store.currentTicket">
            <UiHeading :level="1" variant="card">
              {{ t('shared.tktIdSubject', { id: store.currentTicket.id, subject: store.currentTicket.subject }) }}
            </UiHeading>
            <UiBadge :tone="ticketStatusTone[store.currentTicket.status]" small>
              {{ ticketStatusLabel(store.currentTicket.status) }}
            </UiBadge>
          </template>
          <UiStack v-else direction="row" :align="UiAlignItems.Center" aria-hidden="true" data-testid="client-ticket-loading">
            <UiSkeletonBar class="h-6 w-64 rounded-md" />
            <UiSkeletonBar class="h-5 w-20 rounded-full" />
          </UiStack>
        </UiStack>
        <UiStack direction="row" gap="sm" :align="UiAlignItems.Center">
          <UiButton
            v-if="store.currentTicket?.status !== TicketStatus.Closed"
            type="button"
            :variant="UiButtonVariant.Secondary"
            :disabled="!store.currentTicket"
            data-testid="client-ticket-close"
            @click="handleClose"
          >
            {{ t('shared.close') }}
          </UiButton>
          <UiButton
            v-else
            type="button"
            :variant="UiButtonVariant.Secondary"
            data-testid="client-ticket-reopen"
            @click="handleReopen"
          >
            {{ t('clients.clientTicketDetail.reopen') }}
          </UiButton>
          <UiButton
            type="button"
            :variant="UiButtonVariant.Danger"
            :disabled="!store.currentTicket"
            data-testid="client-ticket-delete"
            @click="showDeleteModal = true"
          >
            {{ t('shared.delete') }}
          </UiButton>
        </UiStack>
      </UiStack>

      <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="client-ticket-feedback" />

      <UiCard padding="md">
        <UiStack>
          <UiHeading :level="2" variant="card">{{ t('clients.clientTicketDetail.ticketSettings') }}</UiHeading>
          <UiGrid :columns="4">
            <UiField :label="t('shared.department')">
              <UiErrorState
                v-if="store.departmentsError !== null"
                :message="store.departmentsError"
                testid="client-ticket-departments-error"
                @retry="store.fetchDepartments"
              />
              <UiSelect v-else v-model="editDepartmentId" :options="departmentOptions" :placeholder="t('shared.selectDepartment')" data-testid="client-ticket-department" />
            </UiField>
            <UiField :label="t('shared.priority')">
              <UiSelect v-model="editPriority" :options="priorityOptions" :placeholder="t('shared.selectPriority')" data-testid="client-ticket-priority" />
            </UiField>
            <UiField :label="t('shared.status')">
              <UiBadge v-if="store.currentTicket" :tone="ticketStatusTone[store.currentTicket.status]">
                {{ ticketStatusLabel(store.currentTicket.status) }}
              </UiBadge>
            </UiField>
            <UiField :label="t('shared.lastReply')">
              <UiText :tone="UiTextTone.Secondary">
                {{ lastReplyAt ? formatDate(lastReplyAt) : t('clients.clientTicketDetail.noRepliesYet') }}
              </UiText>
            </UiField>
          </UiGrid>
          <UiStack direction="row">
            <UiButton
              type="button"
              :variant="UiButtonVariant.Primary"
              :loading="store.loading"
              :disabled="!store.currentTicket"
              data-testid="client-ticket-save"
              @click="handleSaveChanges"
            >
              {{ t('shared.saveChanges') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiCard>

      <UiCard padding="md">
        <UiStack>
          <UiHeading :level="2" variant="card">{{ t('shared.addReply') }}</UiHeading>
          <UiField :label="t('shared.message')">
            <template #default="{ id }">
              <UiTextarea
                :id="id"
                v-model="replyMessage"
                rows="5"
                :placeholder="t('clients.clientTicketDetail.typeYourReply')"
                data-testid="client-ticket-message"
              />
            </template>
          </UiField>
          <UiStack direction="row">
            <UiButton
              type="button"
              :variant="UiButtonVariant.Primary"
              :loading="store.loading"
              :disabled="replyIncomplete || !store.currentTicket"
              data-testid="client-ticket-reply"
              @click="handleReply"
            >
              {{ t('clients.clientTicketDetail.reply') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiCard>

      <UiCard padding="md">
        <UiStack data-testid="client-ticket-thread">
          <UiHeading :level="2" variant="card">{{ t('clients.clientTicketDetail.conversation') }}</UiHeading>

          <template v-if="!store.currentTicket">
            <UiChatBubble :side="UiChatBubbleSide.Other" loading data-testid="client-ticket-message-loading" />
            <UiChatBubble :side="UiChatBubbleSide.Own" loading data-testid="client-ticket-message-loading" />
          </template>

          <UiCard v-else padding="sm">
            <UiStack gap="sm">
              <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Sm">{{ t('shared.client') }}</UiText>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ formatDateTimeCompact(store.currentTicket.createdAt) }}</UiText>
              </UiStack>
              <UiText :tone="UiTextTone.Secondary" class="whitespace-pre-wrap">{{ store.currentTicket.message }}</UiText>
            </UiStack>
          </UiCard>

          <UiCard v-for="reply in store.currentTicket?.replies ?? []" :key="reply.id" padding="sm">
            <UiStack gap="sm">
              <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
                <UiStack direction="row" gap="sm" :align="UiAlignItems.Center">
                  <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Sm">{{ orUnknown(reply.authorName) }}</UiText>
                  <UiBadge v-if="reply.isStaffReply" :tone="UiTone.Primary" small>
                    {{ t('clients.clientTicketDetail.staff') }}
                  </UiBadge>
                </UiStack>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ formatDateTimeCompact(reply.createdAt) }}</UiText>
              </UiStack>
              <UiText :tone="UiTextTone.Secondary" class="whitespace-pre-wrap">{{ reply.message }}</UiText>
            </UiStack>
          </UiCard>

          <UiText v-if="store.currentTicket?.replies.length === 0" :tone="UiTextTone.Muted">
            {{ t('clients.clientTicketDetail.noRepliesYet') }}
          </UiText>
        </UiStack>
      </UiCard>
    </template>

    <UiDialog
      v-if="showDeleteModal"
      variant="confirm"
      :title="t('clients.clientTicketDetail.deleteTicket')"
      :message="t('clients.clientTicketDetail.confirmDeleteTicket', { id: ticketId })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      tone="danger"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
