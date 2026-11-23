<script setup lang="ts">
/**
 * Open a new ticket: the staff form that creates a support ticket on behalf of a client, with a
 * client search, department and priority. Opens the ticket's detail page on success; a refusal is toasted
 * with the API's sentence.
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { TICKET_PRIORITY_OPTIONS } from '@/lib/constants'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useClientsStore } from '@/stores/clients'
import { useSupportStore } from '@/stores/support'
import { TicketPriority } from '@/types/ticket'
import type { ClientListItem } from '@/types/client'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import schema from '@/views/SupportOpenTicketView.schema'
import { AppRoute, supportTicketPath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

const router = useRouter()

/** Toasts the refusal of the creation. */
const feedback = useOutcomeToast()
const store = useSupportStore()
const clientsStore = useClientsStore()

/** Shortest search text that triggers a client lookup. */
const MIN_SEARCH_LENGTH = 2

/** Search query for client lookup. */
const clientSearch = ref('')

/** Whether the client search results are visible. */
const showClientDropdown = ref(false)

/** Search results from the clients store. */
const clientResults = ref<ClientListItem[]>([])

/** Selected client ID. */
const selectedClientId = ref<number | null>(null)

/** Auto-filled client name. */
const clientName = ref('')

/** Auto-filled client email. */
const clientEmail = ref('')

/** Ticket subject. */
const subject = ref('')

/** Selected department ID; `null` while none is chosen, so the API answers with the field error. */
const departmentId = ref<number | null>(null)

/** Selected priority. */
const priority = ref<TicketPriority>(TicketPriority.Medium)

/** Ticket message body. */
const message = ref('')

/** Department options mapped for UiSelect. */
const departmentOptions = computed(() => store.departments.map((department) => ({ value: department.id, label: department.name })))

/** Whether the form can be submitted. */
const canSubmit = computed<boolean>(
  () =>
    selectedClientId.value !== null &&
    subject.value.trim().length > 0 &&
    message.value.trim().length > 0,
)

/** Debounce timer for client search. */
let searchTimer: ReturnType<typeof setTimeout> | undefined

/**
 * Takes the typed search text and looks clients up once typing pauses.
 *
 * @param value - The text in the client search box.
 */
const handleClientSearch = (value: string): void => {
  clientSearch.value = value
  clearTimeout(searchTimer)

  if (value.trim().length < MIN_SEARCH_LENGTH) {
    clientResults.value = []
    showClientDropdown.value = false
    return
  }

  searchTimer = setTimeout(async () => {
    const items = await clientsStore.searchClients(value.trim())
    clientResults.value = items
    showClientDropdown.value = items.length > 0
  }, SEARCH_DEBOUNCE_MS)
}

/**
 * Picks a client from the results and fills the read-only fields.
 *
 * @param client - The chosen client.
 */
const selectClient = (client: ClientListItem): void => {
  selectedClientId.value = client.id
  clientSearch.value = `${client.firstName} ${client.lastName} (#${client.id})`
  clientName.value = `${client.firstName} ${client.lastName}`
  clientEmail.value = client.email
  showClientDropdown.value = false
  clientResults.value = []
}

/**
 * Clears the chosen client and the fields filled from it.
 */
const clearClient = (): void => {
  selectedClientId.value = null
  clientSearch.value = ''
  clientName.value = ''
  clientEmail.value = ''
}

/**
 * Creates the ticket and opens it.
 */
const handleSubmit = async (): Promise<void> => {
  if (!canSubmit.value || selectedClientId.value === null) {
    return
  }
  const newId = await store.createTicket({
    clientId: selectedClientId.value,
    subject: subject.value.trim(),
    message: message.value.trim(),
    departmentId: departmentId.value,
    priority: priority.value,
  })
  if (newId) {
    router.push(supportTicketPath(newId))
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

onBeforeUnmount(() => {
  clearTimeout(searchTimer)
})

onMounted(() => {
  store.fetchDepartments()
})
</script>

<template>
  <UiStack>
    <UiPageHeader
      v-bind="schemaHeader(schema, t)"
      :back-to="AppRoute.SupportTickets"
      :back-label="t('shared.tickets')"
      back-testid="support-open-ticket-back"
    />

    <UiSchemaForm :columns="schema.columns" :errors="store.fieldErrors">
      <template #field-clientId>
        <UiStack class="relative" :gap="UiGap.Xs">
          <UiInput
            :model-value="clientSearch"
            :placeholder="t('support.supportOpenTicket.searchByNameEmailOr')"
            data-testid="support-open-ticket-client"
            @update:model-value="handleClientSearch"
            @focus="showClientDropdown = clientResults.length > 0"
          />
          <UiStack v-if="selectedClientId !== null" :direction="UiDirection.Row" :justify="UiJustify.End">
            <UiButton
              :variant="UiButtonVariant.Link"
              :size="UiSize.Sm"
              data-testid="support-open-ticket-clear-client"
              @click="clearClient"
            >
              {{ t('shared.clear') }}
            </UiButton>
          </UiStack>
          <UiCard
            v-if="showClientDropdown"
            elevated
            class="absolute z-10 top-full mt-1 w-full"
            data-testid="support-open-ticket-client-results"
          >
            <UiButton
              v-for="client in clientResults"
              :key="client.id"
              :variant="UiButtonVariant.Ghost"
              block
              class="justify-start"
              :data-testid="`support-open-ticket-client-${client.id}`"
              @click="selectClient(client)"
            >
              <UiText :as="UiTextTag.Span" :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ client.firstName }} {{ client.lastName }}</UiText>
              <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Md">#{{ client.id }}</UiText>
              <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Md">{{ client.email }}</UiText>
            </UiButton>
          </UiCard>
        </UiStack>
      </template>
      <template #field-name>
        <UiInput
          :model-value="clientName"
          readonly
          :placeholder="t('support.supportOpenTicket.autoFilledFromSelectedClient')"
          data-testid="support-open-ticket-name"
        />
      </template>
      <template #field-email>
        <UiInput
          :model-value="clientEmail"
          readonly
          :placeholder="t('support.supportOpenTicket.autoFilledFromSelectedClient')"
          data-testid="support-open-ticket-email"
        />
      </template>
      <template #field-subject>
        <UiInput
          v-model="subject"
          :placeholder="t('support.supportOpenTicket.ticketSubject')"
          data-testid="support-open-ticket-subject"
        />
      </template>
      <template #field-departmentId>
        <UiErrorState
          v-if="store.departmentsError !== null"
          :message="store.departmentsError"
          testid="support-open-ticket-departments-error"
          @retry="store.fetchDepartments"
        />
        <UiSelect
          v-else
          v-model="departmentId"
          :options="departmentOptions"
          :placeholder="t('shared.selectDepartment')"
          data-testid="support-open-ticket-department"
        />
      </template>
      <template #field-priority>
        <UiSelect
          v-model="priority"
          :options="TICKET_PRIORITY_OPTIONS"
          :placeholder="t('shared.selectPriority')"
          data-testid="support-open-ticket-priority"
        />
      </template>
      <template #field-message>
        <UiTextarea
          v-model="message"
          rows="10"
          :placeholder="t('support.supportOpenTicket.describeTheIssue')"
          data-testid="support-open-ticket-message"
        />
      </template>

      <UiStack :direction="UiDirection.Row">
        <UiButton :action="UiActionKind.Add"
          :variant="UiButtonVariant.Primary"
          :loading="store.loading"
          :disabled="!canSubmit"
          data-testid="support-open-ticket-submit"
          @click="handleSubmit"
        >
          {{ t('shared.createTicket') }}
        </UiButton>
      </UiStack>
    </UiSchemaForm>
  </UiStack>
</template>
