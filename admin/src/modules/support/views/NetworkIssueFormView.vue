<script setup lang="ts">
/**
 * Network issue create/edit form -- shared for both new and edit modes.
 * Detects mode from route params: if `:id` is present, loads existing issue.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '../../../composables/useApi'
import AppSelect from '../../../components/AppSelect.vue'
import AppDatePicker from '../../../components/AppDatePicker.vue'

/** Shape of the network issue form data. */
interface NetworkIssueForm {
  /** Issue title. */
  title: string
  /** Issue type. */
  type: string
  /** Affected server name (optional). */
  server: string
  /** Priority level. */
  priority: string
  /** Current status (edit mode only). */
  status: string
  /** ISO datetime-local string for start date. */
  startDate: string
  /** ISO datetime-local string for end date (optional). */
  endDate: string
  /** Detailed description of the issue. */
  description: string
}

const route = useRoute()
const router = useRouter()
const { request } = useApi()

/** Whether we are editing an existing issue. */
const isEditMode = computed(() => !!route.params.id)

/** The issue ID when in edit mode. */
const issueId = computed(() => route.params.id ? Number(route.params.id) : null)

/** Page title reflecting current mode. */
const pageTitle = computed(() => isEditMode.value ? 'Edit Issue' : 'Create New Issue')

/** Type select options. */
const typeOptions = [
  { value: 'Server', label: 'Server' },
  { value: 'Other', label: 'Other' },
]

/** Priority select options. */
const priorityOptions = [
  { value: 'Low', label: 'Low' },
  { value: 'Medium', label: 'Medium' },
  { value: 'High', label: 'High' },
  { value: 'Critical', label: 'Critical' },
]

/** Status select options (edit mode only). */
const statusOptions = [
  { value: 'Reported', label: 'Reported' },
  { value: 'Investigating', label: 'Investigating' },
  { value: 'Scheduled', label: 'Scheduled' },
  { value: 'Resolved', label: 'Resolved' },
]

/** Form data. */
const form = ref<NetworkIssueForm>({
  title: '',
  type: 'Server',
  server: '',
  priority: 'Medium',
  status: 'Reported',
  startDate: new Date().toISOString().slice(0, 10),
  endDate: '',
  description: '',
})

/** Whether the form is being submitted. */
const saving = ref(false)

/** Whether the existing issue is being loaded. */
const loading = ref(false)

/** Error message from the last operation. */
const error = ref<string | null>(null)

/**
 * Converts an ISO date string to the datetime-local input format.
 *
 * @param iso - ISO 8601 date string or null.
 * @returns String in YYYY-MM-DDTHH:MM format, or empty string.
 */
function toDateTimeLocal(iso: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return d.toISOString().slice(0, 16)
}

/**
 * Fetches an existing network issue for editing.
 */
async function fetchIssue(): Promise<void> {
  if (!issueId.value) return
  loading.value = true
  error.value = null
  try {
    const data = await request<{
      id: number
      title: string
      type: string
      server: string
      priority: string
      status: string
      startDate: string
      endDate: string | null
      description: string
    }>(`/network-issues/${issueId.value}`)
    form.value = {
      title: data.title,
      type: data.type,
      server: data.server ?? '',
      priority: data.priority,
      status: data.status,
      startDate: toDateTimeLocal(data.startDate),
      endDate: toDateTimeLocal(data.endDate),
      description: data.description ?? '',
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load network issue'
  } finally {
    loading.value = false
  }
}

/**
 * Submits the form -- creates or updates based on current mode.
 */
async function handleSave(): Promise<void> {
  saving.value = true
  error.value = null
  try {
    const payload = {
      title: form.value.title,
      type: form.value.type,
      server: form.value.server || null,
      priority: form.value.priority,
      status: form.value.status,
      startDate: form.value.startDate ? new Date(form.value.startDate).toISOString() : null,
      endDate: form.value.endDate ? new Date(form.value.endDate).toISOString() : null,
      description: form.value.description,
    }

    if (isEditMode.value && issueId.value) {
      await request(`/network-issues/${issueId.value}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
    } else {
      await request('/network-issues', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    }

    router.push('/support/network-issues')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to save network issue'
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  if (isEditMode.value) {
    fetchIssue()
  }
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Breadcrumb -->
    <div class="flex items-center gap-2 text-[0.78rem] text-text-muted mb-4">
      <RouterLink to="/support/network-issues" class="text-primary-400 hover:underline">
        Network Issues
      </RouterLink>
      <span>/</span>
      <span class="text-text-secondary">{{ pageTitle }}</span>
    </div>

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">{{ pageTitle }}</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">
        {{ isEditMode ? 'Update the network issue details below' : 'Report a new network issue or scheduled maintenance' }}
      </p>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading network issue...
    </div>

    <!-- Error -->
    <div v-if="error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-4">
      {{ error }}
    </div>

    <!-- Form -->
    <div v-if="!loading" class="bg-surface-card border border-border rounded-2xl p-6 space-y-5">

      <!-- Title -->
      <div>
        <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Title</label>
        <input
          v-model="form.title"
          type="text"
          placeholder="Issue title"
          class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
        />
      </div>

      <!-- Type & Server row -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <!-- Type -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Type</label>
          <AppSelect
            v-model="form.type"
            :options="typeOptions"
            placeholder="Select type"
          />
        </div>

        <!-- Server -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Server</label>
          <input
            v-model="form.server"
            type="text"
            placeholder="Server name (optional)"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>
      </div>

      <!-- Priority & Status row -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <!-- Priority -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Priority</label>
          <AppSelect
            v-model="form.priority"
            :options="priorityOptions"
            placeholder="Select priority"
          />
        </div>

        <!-- Status (edit mode only) -->
        <div v-if="isEditMode">
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
          <AppSelect
            v-model="form.status"
            :options="statusOptions"
            placeholder="Select status"
          />
        </div>
      </div>

      <!-- Start Date & End Date row -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <!-- Start Date -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Start Date</label>
          <AppDatePicker v-model="form.startDate" placeholder="Select start date" />
        </div>

        <!-- End Date -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">End Date</label>
          <AppDatePicker v-model="form.endDate" placeholder="Select end date" />
        </div>
      </div>

      <!-- Description -->
      <div>
        <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Description</label>
        <textarea
          v-model="form.description"
          placeholder="Describe the issue in detail..."
          class="w-full min-h-[300px] bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
        />
      </div>

      <!-- Actions -->
      <div class="flex items-center gap-3 pt-2">
        <button
          type="button"
          :disabled="saving || !form.title.trim()"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="handleSave"
        >
          <span v-if="saving" class="flex items-center gap-2">
            <span class="w-3.5 h-3.5 rounded-full border-2 border-white/20 border-t-white animate-spin" />
            Saving...
          </span>
          <span v-else>{{ isEditMode ? 'Update Issue' : 'Create Issue' }}</span>
        </button>
        <RouterLink
          to="/support/network-issues"
          class="px-4 py-2 text-[0.82rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.06] transition-colors"
        >
          Cancel
        </RouterLink>
      </div>

    </div>

  </div>
</template>
