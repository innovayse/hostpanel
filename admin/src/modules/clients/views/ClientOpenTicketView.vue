<script setup lang="ts">
/**
 * Open New Ticket page -- form to create a support ticket on behalf of a client.
 * Navigates to the ticket detail page on success.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useClientTicketStore } from '../stores/clientTicketStore'
import { TICKET_PRIORITY_OPTIONS } from '../../../utils/constants'
import AppSelect from '../../../components/AppSelect.vue'

const route = useRoute()
const router = useRouter()
const store = useClientTicketStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

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

/**
 * Submits the new ticket form.
 *
 * @returns Promise that resolves when creation completes.
 */
async function handleSubmit(): Promise<void> {
  if (!subject.value.trim() || !message.value.trim() || !departmentId.value) return

  const newId = await store.createTicket({
    clientId: Number(clientId.value),
    subject: subject.value.trim(),
    message: message.value.trim(),
    departmentId: departmentId.value,
    priority: priority.value,
  })

  if (newId) {
    router.push(`/clients/${clientId.value}/tickets/${newId}`)
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
        :to="`/clients/${clientId}/tickets`"
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
            rows="8"
            placeholder="Describe the issue..."
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
            style="min-height: 250px"
          />
        </div>

        <!-- Submit -->
        <div>
          <button
            type="button"
            :disabled="store.loading || !subject.trim() || !message.trim() || !departmentId"
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
