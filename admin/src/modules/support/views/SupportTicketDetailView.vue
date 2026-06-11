<script setup lang="ts">
/**
 * Support ticket detail page -- displays full ticket data with admin controls,
 * reply form, and conversation thread. Supports status changes, priority/department
 * updates, and ticket deletion via the global support store.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSupportStore } from '../stores/supportStore'
import { formatDate } from '../../../utils/format'
import { TICKET_STATUS_STYLES, TICKET_PRIORITY_OPTIONS, TICKET_PRIORITY_STYLES } from '../../../utils/constants'
import AppSelect from '../../../components/AppSelect.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'

const route = useRoute()
const router = useRouter()
const store = useSupportStore()

/** Ticket ID from route params. */
const ticketId = computed(() => route.params.id as string)

/** Editable department ID for admin controls. */
const editDepartmentId = ref<number>(0)

/** Editable priority for admin controls. */
const editPriority = ref('Medium')

/** Reply message text. */
const replyMessage = ref('')

/** Reply author name. */
const replyAuthorName = ref('')

/** Whether the delete confirmation modal is visible. */
const showDeleteModal = ref(false)

/** Department options mapped for AppSelect. */
const departmentOptions = computed(() =>
  store.departments.map(d => ({ value: d.id, label: d.name })),
)

/** Priority options for AppSelect. */
const priorityOptions = TICKET_PRIORITY_OPTIONS

/**
 * Returns Tailwind classes for a ticket status badge.
 *
 * @param status - The ticket status string.
 * @returns Tailwind class string.
 */
function statusClass(status: string): string {
  return TICKET_STATUS_STYLES[status] ?? 'text-text-muted bg-white/[0.04] border border-border'
}

/**
 * Returns Tailwind classes for a ticket priority badge.
 *
 * @param prio - The ticket priority string.
 * @returns Tailwind class string.
 */
function priorityClass(prio: string): string {
  return TICKET_PRIORITY_STYLES[prio] ?? 'text-text-muted bg-white/[0.04] border border-border'
}

/**
 * Returns a human-readable label for a status value.
 *
 * @param status - The ticket status string.
 * @returns Display label.
 */
function statusLabel(status: string): string {
  if (status === 'AwaitingReply') return 'Awaiting Reply'
  return status
}

/**
 * Formats a datetime string for display in the conversation thread.
 *
 * @param iso - ISO 8601 timestamp.
 * @returns Formatted date string.
 */
function formatDateTime(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

/** Last reply timestamp from the ticket's replies. */
const lastReplyAt = computed(() => {
  const t = store.currentTicket
  if (!t || t.replies.length === 0) return null
  return t.replies[t.replies.length - 1]!.createdAt
})

/**
 * Populates editable fields from the loaded ticket.
 */
function populateForm(): void {
  const t = store.currentTicket
  if (!t) return
  editDepartmentId.value = t.departmentId ?? 0
  editPriority.value = t.priority
}

/**
 * Saves admin control changes (department, priority).
 *
 * @returns Promise that resolves when update completes.
 */
async function handleSaveChanges(): Promise<void> {
  const payload: { departmentId?: number; priority?: string } = {}
  if (editDepartmentId.value) payload.departmentId = editDepartmentId.value
  payload.priority = editPriority.value

  const ok = await store.updateTicket(ticketId.value, payload)
  if (ok) {
    await store.fetchById(ticketId.value)
  }
}

/**
 * Closes an open ticket by setting status to Closed.
 *
 * @returns Promise that resolves when update completes.
 */
async function handleClose(): Promise<void> {
  const ok = await store.updateTicket(ticketId.value, { status: 'Closed' })
  if (ok) {
    await store.fetchById(ticketId.value)
  }
}

/**
 * Reopens a closed ticket by setting status to Open.
 *
 * @returns Promise that resolves when update completes.
 */
async function handleReopen(): Promise<void> {
  const ok = await store.updateTicket(ticketId.value, { status: 'Open' })
  if (ok) {
    await store.fetchById(ticketId.value)
  }
}

/**
 * Submits a staff reply to the ticket.
 *
 * @returns Promise that resolves when reply is sent.
 */
async function handleReply(): Promise<void> {
  if (!replyMessage.value.trim() || !replyAuthorName.value.trim()) return

  const ok = await store.replyToTicket(ticketId.value, replyMessage.value.trim(), replyAuthorName.value.trim())
  if (ok) {
    replyMessage.value = ''
    await store.fetchById(ticketId.value)
  }
}

/**
 * Deletes the ticket and navigates back to the tickets list.
 *
 * @returns Promise that resolves when deletion completes.
 */
async function handleDelete(): Promise<void> {
  showDeleteModal.value = false
  const ok = await store.deleteTicket(ticketId.value)
  if (ok) {
    router.push('/support/tickets')
  }
}

// Populate form when ticket loads
watch(() => store.currentTicket, () => {
  populateForm()
})

onMounted(() => {
  store.fetchById(ticketId.value)
  store.fetchDepartments()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Loading -->
    <div v-if="store.loading && !store.currentTicket" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading ticket...
    </div>

    <!-- Error (only when no data) -->
    <div v-else-if="store.error && !store.currentTicket" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ store.error }}
    </div>

    <template v-else-if="store.currentTicket">

      <!-- Header -->
      <div class="flex items-center justify-between gap-2.5 mb-5 flex-wrap">
        <div class="flex items-center gap-3">
          <RouterLink
            to="/support/tickets"
            class="px-3 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors flex items-center gap-1.5 no-underline"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="15 18 9 12 15 6"/>
            </svg>
            Back
          </RouterLink>
          <h1 class="text-[0.875rem] font-semibold text-text-primary">
            #TKT-{{ store.currentTicket.id }} - {{ store.currentTicket.subject }}
          </h1>
          <span
            class="inline-block px-2.5 py-0.5 rounded-full text-[0.7rem] font-medium"
            :class="statusClass(store.currentTicket.status)"
          >
            {{ statusLabel(store.currentTicket.status) }}
          </span>
        </div>
        <div class="flex items-center gap-2">
          <button
            v-if="store.currentTicket.status !== 'Closed'"
            type="button"
            class="px-4 py-2 text-[0.82rem] font-medium text-status-yellow bg-status-yellow/10 border border-status-yellow/20 rounded-[10px] hover:bg-status-yellow/20 transition-colors"
            @click="handleClose"
          >
            Close
          </button>
          <button
            v-else
            type="button"
            class="px-4 py-2 text-[0.82rem] font-medium text-status-green bg-status-green/10 border border-status-green/20 rounded-[10px] hover:bg-status-green/20 transition-colors"
            @click="handleReopen"
          >
            Reopen
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-[10px] hover:bg-status-red/20 transition-colors"
            @click="showDeleteModal = true"
          >
            Delete
          </button>
        </div>
      </div>

      <!-- Error feedback -->
      <div v-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
        {{ store.error }}
      </div>

      <!-- Admin Controls Card -->
      <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
        <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Ticket Settings</h2>
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Department</label>
            <AppSelect v-model="editDepartmentId" :options="departmentOptions" placeholder="Select department" />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Priority</label>
            <AppSelect v-model="editPriority" :options="priorityOptions" placeholder="Select priority" />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Status</label>
            <span
              class="inline-block px-2.5 py-1.5 rounded-full text-[0.75rem] font-medium mt-0.5"
              :class="statusClass(store.currentTicket.status)"
            >
              {{ statusLabel(store.currentTicket.status) }}
            </span>
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Last Reply</label>
            <span class="text-[0.82rem] text-text-secondary">
              {{ lastReplyAt ? formatDate(lastReplyAt) : 'No replies yet' }}
            </span>
          </div>
        </div>
        <div class="mt-4">
          <button
            type="button"
            :disabled="store.loading"
            class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            @click="handleSaveChanges"
          >
            {{ store.loading ? 'Saving...' : 'Save Changes' }}
          </button>
        </div>
      </div>

      <!-- Reply Section Card -->
      <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
        <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Add Reply</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Author Name</label>
            <input
              v-model="replyAuthorName"
              type="text"
              placeholder="Your name"
              class="w-full max-w-sm bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Message</label>
            <textarea
              v-model="replyMessage"
              rows="5"
              placeholder="Type your reply..."
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y min-h-[150px]"
            />
          </div>
          <div>
            <button
              type="button"
              :disabled="store.loading || !replyMessage.trim() || !replyAuthorName.trim()"
              class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
              @click="handleReply"
            >
              {{ store.loading ? 'Sending...' : 'Reply' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Conversation Thread -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <div class="px-5 py-3 border-b border-border bg-white/[0.02]">
          <h2 class="text-[0.82rem] font-semibold text-text-primary">Conversation</h2>
        </div>

        <!-- Initial message -->
        <div class="px-5 py-4 border-b border-border bg-white/[0.02] border-l-4 border-l-text-muted">
          <div class="flex items-center justify-between mb-2">
            <span class="text-[0.78rem] font-semibold text-text-primary">Client</span>
            <span class="text-[0.72rem] text-text-muted">{{ formatDateTime(store.currentTicket.createdAt) }}</span>
          </div>
          <p class="text-[0.82rem] text-text-secondary whitespace-pre-wrap">{{ store.currentTicket.message }}</p>
        </div>

        <!-- Replies -->
        <div
          v-for="reply in store.currentTicket.replies"
          :key="reply.id"
          class="px-5 py-4 border-b border-border last:border-0 border-l-4"
          :class="reply.isStaffReply ? 'bg-primary-500/5 border-l-primary-500' : 'bg-white/[0.02] border-l-text-muted'"
        >
          <div class="flex items-center justify-between mb-2">
            <div class="flex items-center gap-2">
              <span class="text-[0.78rem] font-semibold text-text-primary">{{ reply.authorName }}</span>
              <span
                v-if="reply.isStaffReply"
                class="text-[0.65rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 px-1.5 py-0.5 rounded-full"
              >
                Staff
              </span>
            </div>
            <span class="text-[0.72rem] text-text-muted">{{ formatDateTime(reply.createdAt) }}</span>
          </div>
          <p class="text-[0.82rem] text-text-secondary whitespace-pre-wrap">{{ reply.message }}</p>
        </div>

        <!-- Empty replies -->
        <div v-if="store.currentTicket.replies.length === 0" class="flex flex-col items-center justify-center py-8 gap-2">
          <p class="text-[0.82rem] text-text-muted">No replies yet</p>
        </div>
      </div>

    </template>

    <!-- Delete Confirmation Modal -->
    <ConfirmModal
      v-if="showDeleteModal"
      title="Delete Ticket"
      :message="`Are you sure you want to delete ticket #TKT-${ticketId}? This action cannot be undone.`"
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="store.loading"
      variant="danger"
      @confirm="handleDelete"
      @close="showDeleteModal = false"
    />
  </div>
</template>
