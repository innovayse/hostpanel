<script setup lang="ts">
/**
 * Open a new ticket: the staff form that creates a support ticket on behalf of a client, with a
 * client search, department and priority. Opens the ticket's detail page on success; a refusal shows
 * the API's sentence next to the submit button.
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { TICKET_PRIORITY_OPTIONS } from '@/lib/constants'
import { useClientsStore } from '@/stores/clients'
import { useSupportStore } from '@/stores/support'
import { TicketPriority } from '@/types/ticket'
import type { ClientListItem } from '@/types/client'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import { AppRoute, supportTicketPath } from '@/lib/routes'

const { t } = useI18n()

const router = useRouter()
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
    <UiPageHeader :title="t('shared.openNewTicket')">
      <template #actions>
        <UiLink
          :to="AppRoute.SupportTickets"
          :variant="UiButtonVariant.Secondary"
          :size="UiSize.Sm"
          data-testid="support-open-ticket-back"
        >
          <UiIcon :markup="Icon.Back" :size="UiSize.Sm" />
          {{ t('shared.back') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiCard padding="lg">
      <UiStack>
        <UiField :label="t('shared.client')" :error="store.fieldErrors.clientId">
          <UiStack class="relative" gap="xs">
            <UiInput
              :model-value="clientSearch"
              :placeholder="t('support.supportOpenTicket.searchByNameEmailOr')"
              data-testid="support-open-ticket-client"
              @update:model-value="handleClientSearch"
              @focus="showClientDropdown = clientResults.length > 0"
            />
            <UiStack v-if="selectedClientId !== null" direction="row" justify="end">
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
                block
                class="justify-start"
                :data-testid="`support-open-ticket-client-${client.id}`"
                @click="selectClient(client)"
              >
                <UiText tone="primary" size="md">{{ client.firstName }} {{ client.lastName }}</UiText>
                <UiText tone="muted" size="md">#{{ client.id }}</UiText>
                <UiText tone="muted" size="md">{{ client.email }}</UiText>
              </UiButton>
            </UiCard>
          </UiStack>
        </UiField>

        <UiGrid :columns="2">
          <UiField :label="t('shared.name')">
            <UiInput
              :model-value="clientName"
              readonly
              :placeholder="t('support.supportOpenTicket.autoFilledFromSelectedClient')"
              data-testid="support-open-ticket-name"
            />
          </UiField>

          <UiField :label="t('shared.emailAddress')">
            <UiInput
              :model-value="clientEmail"
              readonly
              :placeholder="t('support.supportOpenTicket.autoFilledFromSelectedClient')"
              data-testid="support-open-ticket-email"
            />
          </UiField>
        </UiGrid>

        <UiField :label="t('shared.subject')" :error="store.fieldErrors.subject">
          <UiInput
            v-model="subject"
            :placeholder="t('support.supportOpenTicket.ticketSubject')"
            data-testid="support-open-ticket-subject"
          />
        </UiField>

        <UiGrid :columns="2">
          <UiField :label="t('shared.department')" :error="store.fieldErrors.departmentId">
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
          </UiField>
          <UiField :label="t('shared.priority')">
            <UiSelect
              v-model="priority"
              :options="TICKET_PRIORITY_OPTIONS"
              :placeholder="t('shared.selectPriority')"
              data-testid="support-open-ticket-priority"
            />
          </UiField>
        </UiGrid>

        <UiField :label="t('shared.message')" :error="store.fieldErrors.message">
          <UiTextarea
            v-model="message"
            rows="10"
            :placeholder="t('support.supportOpenTicket.describeTheIssue')"
            data-testid="support-open-ticket-message"
          />
        </UiField>

        <UiStack direction="row">
          <UiButton
            :variant="UiButtonVariant.Primary"
            :loading="store.loading"
            :disabled="!canSubmit"
            data-testid="support-open-ticket-submit"
            @click="handleSubmit"
          >
            {{ t('support.supportOpenTicket.openTicket') }}
          </UiButton>
        </UiStack>
        <UiStatusMessage :message="store.actionError" :tone="UiTone.Danger" data-testid="support-open-ticket-error" />
      </UiStack>
    </UiCard>
  </UiStack>
</template>
