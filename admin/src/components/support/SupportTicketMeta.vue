<script setup lang="ts">
/**
 * The details of a ticket and the two things staff can change on it: its department and priority.
 * It sits in a side card beside the thread on a wide screen and in a bottom sheet on a phone; the
 * screen owns the edited values and the save, this component only draws them. Without a ticket (still being read) every
 * field keeps its real caption and draws its control as a placeholder.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import SupportTicketStatusBadge from '@/components/support/SupportTicketStatusBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { TICKET_PRIORITY_OPTIONS } from '@/lib/constants'
import { formatDateTimeCompact } from '@/lib/format'
import type { Ticket, TicketPriority } from '@/types/ticket'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

const { t } = useI18n()

/** Props for SupportTicketMeta. */
const props = withDefaults(defineProps<{
  /** The ticket whose details are shown; null while it is being read. */
  ticket: Ticket | null
  /** Departments the ticket can be moved to, ready for the select. */
  departmentOptions: { value: number; label: string }[]
  /** Sentence of the refused department lookup, null when it loaded; shown with a retry. */
  departmentsError: string | null
  /** Timestamp of the latest reply, null while there is none. */
  lastReplyAt: string | null
  /** Whether the save is running. */
  saving: boolean
  /** Outcome sentence of the last save, null for none. */
  feedback?: string | null
  /** Tone of the outcome sentence. */
  feedbackTone?: UiTone
}>(), {
  feedback: null,
  feedbackTone: UiTone.Success,
})

const emit = defineEmits<{
  /** The operator pressed Save. */
  save: []
  /** The operator asked to load the departments again. */
  retryDepartments: []
}>()

/** True while the ticket is still being read, so the fields draw placeholders. */
const loading = computed<boolean>(() => props.ticket === null)

/** Department the ticket is being moved to (0 = none chosen). */
const departmentId = defineModel<number>('departmentId', { required: true })

/** Priority the ticket is being given. */
const priority = defineModel<TicketPriority>('priority', { required: true })
</script>

<template>
  <UiStack data-testid="ticket-meta">
    <UiField :label="t('shared.department')" :loading="loading">
      <UiErrorState
        v-if="departmentsError !== null"
        :message="departmentsError"
        testid="ticket-departments-error"
        @retry="emit('retryDepartments')"
      />
      <UiSelect
        v-else
        v-model="departmentId"
        :options="departmentOptions"
        :placeholder="t('shared.selectDepartment')"
        data-testid="ticket-department"
      />
    </UiField>
    <UiField :label="t('shared.priority')" :loading="loading">
      <UiSelect
        v-model="priority"
        :options="TICKET_PRIORITY_OPTIONS"
        :placeholder="t('shared.selectPriority')"
        data-testid="ticket-priority"
      />
    </UiField>
    <UiButton
      :variant="UiButtonVariant.Primary"
      :loading="saving"
      data-testid="ticket-save"
      @click="emit('save')"
    >
      {{ t('shared.saveChanges') }}
    </UiButton>
    <UiStatusMessage :message="feedback" :tone="feedbackTone" data-testid="ticket-save-feedback" />

    <UiField :label="t('shared.status')" :loading="loading">
      <SupportTicketStatusBadge v-if="ticket" :status="ticket.status" />
    </UiField>
    <UiField :label="t('support.supportTicketDetail.assignee')" :loading="loading">
      <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md" data-testid="ticket-assignee">
        {{ ticket?.assignee.name ?? t('support.supportTicketDetail.unassigned') }}
      </UiText>
    </UiField>
    <UiField :label="t('support.supportTicketDetail.created')" :loading="loading">
      <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md" data-testid="ticket-created">
        {{ ticket ? formatDateTimeCompact(ticket.createdAt) : '' }}
      </UiText>
    </UiField>
    <UiField :label="t('shared.lastReply')" :loading="loading">
      <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md" data-testid="ticket-last-reply">
        {{ lastReplyAt ? formatDateTimeCompact(lastReplyAt) : t('support.supportTicketDetail.noRepliesYet') }}
      </UiText>
    </UiField>
  </UiStack>
</template>
