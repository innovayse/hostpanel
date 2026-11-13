<template>
  <UiStack gap="lg" class="max-w-2xl">
    <UiBackLink :to="CLIENT_TICKETS_PATH" :label="$t('client.tickets.backTo')" testid="ticket-back" />

    <UiPageHeader :title="$t('client.tickets.newTitle')" :description="$t('client.tickets.newSubtitle')" class="!mb-0" />

    <UiCard v-if="submitted" data-testid="ticket-success">
      <UiEmptyState :icon="CheckCircle" :title="$t('client.tickets.successTitle')" :description="$t('client.tickets.successSubtitle')">
        <UiButton :to="CLIENT_TICKETS_PATH" data-testid="ticket-view-all">{{ $t('client.tickets.viewMyTickets') }}</UiButton>
      </UiEmptyState>
    </UiCard>

    <UiCard v-else>
      <UiForm data-testid="ticket-new-form" :error="ticketError" spacing="lg" @submit="handleSubmit">
        <UiSelect
          v-model="form.departmentId"
          :label="$t('client.tickets.deptLabel')"
          :placeholder="$t('client.tickets.deptPlaceholder')"
          :options="deptOptions"
          :disabled="deptPending"
          :error="fieldErrors.departmentId"
          testid="ticket-department"
          required
        />

        <UiInput
          v-model="form.subject"
          :label="$t('client.tickets.subjectLabel')"
          :placeholder="$t('client.tickets.subjectPlaceholder')"
          testid="ticket-subject"
          required
        />

        <ClientSupportTicketPriorityPicker v-model="form.priority" />

        <UiTextarea
          v-model="form.message"
          :label="$t('client.tickets.messageLabel')"
          :placeholder="$t('client.tickets.messagePlaceholder')"
          :rows="6"
          :resize="false"
          testid="ticket-message"
          required
        />

        <template #actions>
          <UiButton type="submit" :loading="ticketLoading" full-width data-testid="ticket-submit">
            <Send v-if="!ticketLoading" :size="16" :stroke-width="2" class="mr-2" />
            {{ $t('client.tickets.submit') }}
          </UiButton>
        </template>
      </UiForm>
    </UiCard>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * New support ticket: department, subject, priority and message, sent through the support store.
 * A refusal is rendered from the store's `ticketError`; an accepted ticket swaps the form for a
 * confirmation.
 *
 * The form keeps a short column (a width cap, an allowed exception: form fields are intrinsically
 * narrow, so this is not page layout; the page width itself comes from the shared container).
 */
import { CLIENT_TICKETS_PATH } from '~/constants/clientRoutes'
import { CheckCircle, Send } from 'lucide-vue-next'
import { useSupportStore } from '~/stores/support'
import { SubmitOutcome } from '~/types/submitoutcome'
import { TicketPriority } from '~/types/ticketpriority'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Submission state of the form; the store owns the loading flag and the refusal sentence. */
const supportStore = useSupportStore()
/** Reactive state of the store this component renders. */
const { ticketLoading, ticketError, fieldErrors, departments } = storeToRefs(supportStore)
/** Action that submits the new ticket. */
const { createTicket } = supportStore

/** Whether the departments are still loading. */
const { pending: deptPending } = useAsyncData('client-ticket-departments', () => supportStore.loadDepartments())

/** Departments in the select's option shape. */
const deptOptions = computed(() => (departments.value ?? []).map((department) => ({ value: department.id, label: department.name })))

/** The form fields. */
const form = reactive({
  departmentId: null as number | null,
  subject: '',
  message: '',
  priority: TicketPriority.Medium,
})

/** True once the API accepted the ticket; swaps the form for the confirmation. */
const submitted = ref(false)

/**
 * Sends the ticket through the store and shows the confirmation when it was accepted.
 *
 * @returns Resolves when the attempt has ended; a refusal is rendered from `ticketError`.
 */
const handleSubmit = async (): Promise<void> => {
  submitted.value = (await createTicket(form)) === SubmitOutcome.Sent
}
</script>
