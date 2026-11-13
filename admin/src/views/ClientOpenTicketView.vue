<script setup lang="ts">
/**
 * Open New Ticket page: form to create a support ticket on behalf of a client.
 * Navigates to the ticket detail page on success.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientTicketsStore } from '@/stores/clientTickets'
import { TICKET_PRIORITY_OPTIONS } from '@/lib/constants'
import { TicketPriority } from '@/types/ticket'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import { ClientSection, clientItemPath, clientSectionPath } from '@/lib/routes'

const { t } = useI18n()

/** Icon markup (shape tags) of the back link. */
const BACK_ICON = '<polyline points="15 18 9 12 15 6"/>'

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open the new ticket. */
const router = useRouter()

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
  }
}

onMounted(() => {
  store.fetchDepartments()
})
</script>

<template>
  <UiStack>
    <UiStack direction="row" :align="UiAlignItems.Center">
      <UiLink
        :to="clientSectionPath(clientId, ClientSection.Tickets)"
        :variant="UiButtonVariant.Secondary"
        data-testid="client-open-ticket-back"
      >
        <UiIcon :markup="BACK_ICON" :size="UiSize.Sm" />
        {{ t('shared.back') }}
      </UiLink>
      <UiHeading :level="1" variant="card">{{ t('shared.openNewTicket') }}</UiHeading>
    </UiStack>

    <UiErrorState
      v-if="store.departmentsError !== null"
      :message="store.departmentsError"
      testid="client-open-ticket-departments-error"
      @retry="store.fetchDepartments"
    />

    <UiCard v-else padding="lg">
      <UiStack>
        <UiField :label="t('shared.subject')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="subject"
              type="text"
              :placeholder="t('clients.clientOpenTicket.ticketSubject')"
              data-testid="client-open-ticket-subject"
            />
          </template>
        </UiField>

        <UiGrid :columns="2">
          <UiField :label="t('shared.department')">
            <UiSelect v-model="departmentId" :options="departmentOptions" :placeholder="t('shared.selectDepartment')" data-testid="client-open-ticket-department" />
          </UiField>
          <UiField :label="t('shared.priority')">
            <UiSelect v-model="priority" :options="TICKET_PRIORITY_OPTIONS" :placeholder="t('shared.selectPriority')" data-testid="client-open-ticket-priority" />
          </UiField>
        </UiGrid>

        <UiField :label="t('shared.message')">
          <template #default="{ id }">
            <UiTextarea
              :id="id"
              v-model="message"
              rows="8"
              :placeholder="t('clients.clientOpenTicket.describeTheIssue')"
              data-testid="client-open-ticket-message"
            />
          </template>
        </UiField>

        <UiStack direction="row">
          <UiButton
            type="button"
            :variant="UiButtonVariant.Primary"
            :loading="store.loading"
            :disabled="incomplete"
            data-testid="client-open-ticket-submit"
            @click="handleSubmit"
          >
            {{ t('clients.clientOpenTicket.openTicket') }}
          </UiButton>
        </UiStack>
        <UiStatusMessage :message="store.error" :tone="UiTone.Danger" data-testid="client-open-ticket-error" />
      </UiStack>
    </UiCard>
  </UiStack>
</template>
