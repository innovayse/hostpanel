<script setup lang="ts">
/**
 * Open New Ticket page: form to create a support ticket on behalf of a client.
 * Navigates to the ticket detail page on success; a refusal is toasted with the API's sentence.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useClientTicketsStore } from '@/stores/clientTickets'
import { TICKET_PRIORITY_OPTIONS } from '@/lib/constants'
import { TicketPriority } from '@/types/ticket'
import UiButton from '@/components/ui/UiButton.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { ClientSection, clientItemPath, clientSectionPath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import schema from '@/views/ClientOpenTicketView.schema'
import { UiActionKind } from '@/utils/ui/actions'
import { UiDirection } from '@/utils/ui/layout'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open the new ticket. */
const router = useRouter()

/** Toasts the refusal of the creation. */
const feedback = useOutcomeToast()

/** Client ticket store, owner of departments, creation and its error. */
const store = useClientTicketsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Ticket subject. */
const subject = ref('')

/** Selected department ID. */
const departmentId = ref<number>(0)

/** Selected priority. */
const priority = ref<TicketPriority>(TicketPriority.Medium)

/** Ticket message body. */
const message = ref('')

/** Department options mapped for UiSelect. */
const departmentOptions = computed(() =>
  store.departments.map(d => ({ value: d.id, label: d.name })),
)

/** True when the form cannot be submitted yet. */
const incomplete = computed<boolean>(() => !subject.value.trim() || !message.value.trim() || !departmentId.value)

/**
 * Submits the new ticket form.
 *
 * @returns Promise that resolves when creation completes.
 */
const handleSubmit = async (): Promise<void> => {
  if (incomplete.value) {
    return
  }

  const newId = await store.createTicket({
    clientId: Number(clientId.value),
    subject: subject.value.trim(),
    message: message.value.trim(),
    departmentId: departmentId.value,
    priority: priority.value,
  })

  if (newId) {
    router.push(clientItemPath(clientId.value, ClientSection.Tickets, newId))
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

onMounted(() => {
  store.fetchDepartments()
})
</script>

<template>
  <UiStack>
    <UiPageHeader
      v-bind="schemaHeader(schema, t)"
      :back-to="clientSectionPath(clientId, ClientSection.Tickets)"
      :back-label="t('shared.tickets')"
      back-testid="client-open-ticket-back"
    />

    <UiErrorState
      v-if="store.departmentsError !== null"
      :message="store.departmentsError"
      testid="client-open-ticket-departments-error"
      @retry="store.fetchDepartments"
    />

    <UiSchemaForm v-else :columns="schema.columns">
      <template #field-subject="{ id }">
        <UiInput
          :id="id"
          v-model="subject"
          type="text"
          :placeholder="t('clients.clientOpenTicket.ticketSubject')"
          data-testid="client-open-ticket-subject"
        />
      </template>
      <template #field-departmentId>
        <UiSelect v-model="departmentId" :options="departmentOptions" :placeholder="t('shared.selectDepartment')" data-testid="client-open-ticket-department" />
      </template>
      <template #field-priority>
        <UiSelect v-model="priority" :options="TICKET_PRIORITY_OPTIONS" :placeholder="t('shared.selectPriority')" data-testid="client-open-ticket-priority" />
      </template>
      <template #field-message="{ id }">
        <UiTextarea
          :id="id"
          v-model="message"
          rows="8"
          :placeholder="t('clients.clientOpenTicket.describeTheIssue')"
          data-testid="client-open-ticket-message"
        />
      </template>

      <UiStack :direction="UiDirection.Row">
        <UiButton :action="UiActionKind.Add"
          type="button"
          :variant="UiButtonVariant.Primary"
          :loading="store.loading"
          :disabled="incomplete"
          data-testid="client-open-ticket-submit"
          @click="handleSubmit"
        >
          {{ t('clients.clientOpenTicket.createTicket') }}
        </UiButton>
      </UiStack>
    </UiSchemaForm>
  </UiStack>
</template>
