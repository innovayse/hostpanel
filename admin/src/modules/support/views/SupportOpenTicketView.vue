<script setup lang="ts">
/**
 * Open New Ticket page -- admin form to create a support ticket on behalf of a client.
 * Includes client search with auto-complete, department/priority selection,
 * and navigates to the ticket detail page on success.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useSupportStore } from '../stores/supportStore'
import { useApi } from '../../../composables/useApi'
import { TICKET_PRIORITY_OPTIONS } from '../../../utils/constants'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'

const router = useRouter()
const store = useSupportStore()
const { request } = useApi()

/** Search query for client lookup. */
const clientSearch = ref('')

/** Whether the client search dropdown is visible. */
const showClientDropdown = ref(false)

/** Search results from the client API. */
const clientResults = ref<{ id: number; firstName: string; lastName: string; email: string }[]>([])

/** Whether a client search is in progress. */
const searchingClients = ref(false)

/** Selected client ID. */
const selectedClientId = ref<number | null>(null)

/** Auto-filled client name. */
const clientName = ref('')

/** Auto-filled client email. */
const clientEmail = ref('')

/** Whether to send an email notification for the ticket. */
const sendEmail = ref(true)

/** Ticket subject. */
const subject = ref('')

/** Selected department ID. */
const departmentId = ref<number>(0)

/** Selected priority. */
const priority = ref('Medium')

/** Ticket message body. */
const message = ref('')

/** Department options mapped for AppSelect. */
const departmentOptions = computed(() =>
  store.departments.map(d => ({ value: d.id, label: d.name })),
)

/** Whether the form can be submitted. */
const canSubmit = computed(() =>
  selectedClientId.value !== null
  && subject.value.trim().length > 0
  && message.value.trim().length > 0
  && departmentId.value > 0,
)

/** Debounce timer for client search. */
let searchTimer: ReturnType<typeof setTimeout> | undefined

/**
 * Handles client search input with debounce.
 */
function handleClientSearch(): void {
  clearTimeout(searchTimer)

  if (clientSearch.value.trim().length < 2) {
    clientResults.value = []
    showClientDropdown.value = false
    return
  }

  searchTimer = setTimeout(async () => {
    searchingClients.value = true
    try {
      const params = new URLSearchParams({
        search: clientSearch.value.trim(),
        page: '1',
        pageSize: '10',
      })
      const result = await request<{ items: { id: number; firstName: string; lastName: string; email: string }[] }>(
        `/clients?${params}`,
      )
      clientResults.value = result.items
      showClientDropdown.value = result.items.length > 0
    } catch {
      clientResults.value = []
      showClientDropdown.value = false
    } finally {
      searchingClients.value = false
    }
  }, 300)
}

/**
 * Selects a client from the search results and populates the form fields.
 *
 * @param client - The selected client object.
 */
function selectClient(client: { id: number; firstName: string; lastName: string; email: string }): void {
  selectedClientId.value = client.id
  clientSearch.value = `${client.firstName} ${client.lastName} (#${client.id})`
  clientName.value = `${client.firstName} ${client.lastName}`
  clientEmail.value = client.email
  showClientDropdown.value = false
  clientResults.value = []
}

/**
 * Clears the selected client and resets related fields.
 */
function clearClient(): void {
  selectedClientId.value = null
  clientSearch.value = ''
  clientName.value = ''
  clientEmail.value = ''
}

/**
 * Submits the new ticket form.
 *
 * @returns Promise that resolves when creation completes.
 */
async function handleSubmit(): Promise<void> {
  if (!canSubmit.value || selectedClientId.value === null) return

  const newId = await store.createTicket({
    clientId: selectedClientId.value,
    subject: subject.value.trim(),
    message: message.value.trim(),
    departmentId: departmentId.value,
    priority: priority.value,
  })

  if (newId) {
    router.push(`/support/tickets/${newId}`)
  }
}

onMounted(() => {
  store.fetchDepartments()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center gap-3 mb-5">
      <RouterLink
        to="/support/tickets"
        class="px-3 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors flex items-center gap-1.5 no-underline"
      >
        <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="15 18 9 12 15 6"/>
        </svg>
        Back
      </RouterLink>
      <h1 class="text-[0.875rem] font-semibold text-text-primary">Open New Ticket</h1>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
      {{ store.error }}
    </div>

    <!-- Form Card -->
    <div class="bg-surface-card border border-border rounded-2xl p-6">
      <div class="space-y-5">

        <!-- Client Search -->
        <div class="relative">
          <label class="block text-[0.75rem] text-text-muted mb-1.5">Client</label>
          <input
            v-model="clientSearch"
            type="text"
            placeholder="Search by name, email, or ID..."
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            @input="handleClientSearch"
            @focus="showClientDropdown = clientResults.length > 0"
          />
          <button
            v-if="selectedClientId !== null"
            type="button"
            class="absolute right-3 top-[calc(50%+8px)] -translate-y-1/2 text-text-muted hover:text-text-secondary text-xs"
            @click="clearClient"
          >
            Clear
          </button>

          <!-- Search Loading -->
          <div v-if="searchingClients" class="absolute right-3 top-[calc(50%+8px)] -translate-y-1/2">
            <span class="w-3.5 h-3.5 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin inline-block" />
          </div>

          <!-- Dropdown -->
          <div
            v-if="showClientDropdown"
            class="absolute z-10 mt-1 w-full bg-surface-card border border-border rounded-xl shadow-lg overflow-hidden"
          >
            <button
              v-for="client in clientResults"
              :key="client.id"
              type="button"
              class="w-full text-left px-4 py-2.5 text-[0.82rem] text-text-secondary hover:bg-white/[0.04] transition-colors border-b border-border last:border-0"
              @click="selectClient(client)"
            >
              <span class="font-medium text-text-primary">{{ client.firstName }} {{ client.lastName }}</span>
              <span class="text-text-muted ml-2">#{{ client.id }}</span>
              <span class="text-text-muted ml-2">{{ client.email }}</span>
            </button>
          </div>
        </div>

        <!-- Name (read-only) -->
        <div>
          <label class="block text-[0.75rem] text-text-muted mb-1.5">Name</label>
          <input
            :value="clientName"
            type="text"
            readonly
            placeholder="Auto-filled from selected client"
            class="w-full bg-white/[0.02] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-secondary placeholder:text-text-muted cursor-not-allowed"
          />
        </div>

        <!-- Email + Send Email checkbox -->
        <div>
          <label class="block text-[0.75rem] text-text-muted mb-1.5">Email Address</label>
          <div class="flex items-center gap-3">
            <input
              :value="clientEmail"
              type="text"
              readonly
              placeholder="Auto-filled from selected client"
              class="flex-1 bg-white/[0.02] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-secondary placeholder:text-text-muted cursor-not-allowed"
            />
            <label class="flex items-center gap-2 text-[0.78rem] text-text-secondary whitespace-nowrap cursor-pointer" @click="sendEmail = !sendEmail">
              <AppCheckbox v-model="sendEmail" />
              Send Email
            </label>
          </div>
        </div>

        <!-- Subject -->
        <div>
          <label class="block text-[0.75rem] text-text-muted mb-1.5">Subject</label>
          <input
            v-model="subject"
            type="text"
            placeholder="Ticket subject"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Department + Priority -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Department</label>
            <AppSelect v-model="departmentId" :options="departmentOptions" placeholder="Select department" />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Priority</label>
            <AppSelect v-model="priority" :options="TICKET_PRIORITY_OPTIONS" placeholder="Select priority" />
          </div>
        </div>

        <!-- Message -->
        <div>
          <label class="block text-[0.75rem] text-text-muted mb-1.5">Message</label>
          <textarea
            v-model="message"
            rows="10"
            placeholder="Describe the issue..."
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y min-h-[250px]"
          />
        </div>

        <!-- Submit -->
        <div>
          <button
            type="button"
            :disabled="store.loading || !canSubmit"
            class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            @click="handleSubmit"
          >
            {{ store.loading ? 'Creating...' : 'Open Ticket' }}
          </button>
        </div>

      </div>
    </div>
  </div>
</template>
