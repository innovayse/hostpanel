<script setup lang="ts">
/**
 * Announcement create/edit form -- shared for both new and edit modes.
 * Detects mode from route params: if `:id` is present, loads existing announcement.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '../../../composables/useApi'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import AppDatePicker from '../../../components/AppDatePicker.vue'

/** Shape of an announcement for the form. */
interface AnnouncementForm {
  /** ISO date-time string for the announcement. */
  date: string
  /** Announcement title. */
  title: string
  /** Full announcement body content. */
  content: string
  /** Whether the announcement is published. */
  published: boolean
}

const route = useRoute()
const router = useRouter()
const { request } = useApi()

/** Whether we are editing an existing announcement. */
const isEditMode = computed(() => !!route.params.id)

/** The announcement ID when in edit mode. */
const announcementId = computed(() => route.params.id ? Number(route.params.id) : null)

/** Page title reflecting current mode. */
const pageTitle = computed(() => isEditMode.value ? 'Edit Announcement' : 'Add Announcement')

/** Form data. */
const form = ref<AnnouncementForm>({
  date: new Date().toISOString().slice(0, 10),
  title: '',
  content: '',
  published: false,
})

/** Whether the form is being submitted. */
const saving = ref(false)

/** Whether the existing announcement is being loaded. */
const loading = ref(false)

/** Error message from the last operation. */
const error = ref<string | null>(null)

/**
 * Converts an ISO date string to the datetime-local input format.
 *
 * @param iso - ISO 8601 date string.
 * @returns String in YYYY-MM-DDTHH:MM format.
 */
function toDateTimeLocal(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return new Date().toISOString().slice(0, 16)
  return d.toISOString().slice(0, 16)
}

/**
 * Fetches an existing announcement for editing.
 */
async function fetchAnnouncement(): Promise<void> {
  if (!announcementId.value) return
  loading.value = true
  error.value = null
  try {
    const data = await request<{
      id: number
      date: string
      title: string
      content: string
      published: boolean
    }>(`/announcements/${announcementId.value}`)
    form.value = {
      date: toDateTimeLocal(data.date),
      title: data.title,
      content: data.content,
      published: data.published,
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load announcement'
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
      date: new Date(form.value.date).toISOString(),
      title: form.value.title,
      content: form.value.content,
      published: form.value.published,
    }

    if (isEditMode.value && announcementId.value) {
      await request(`/announcements/${announcementId.value}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
    } else {
      await request('/announcements', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    }

    router.push('/support/announcements')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to save announcement'
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  if (isEditMode.value) {
    fetchAnnouncement()
  }
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Breadcrumb -->
    <div class="flex items-center gap-2 text-[0.78rem] text-text-muted mb-4">
      <RouterLink to="/support/announcements" class="text-primary-400 hover:underline">
        Announcements
      </RouterLink>
      <span>/</span>
      <span class="text-text-secondary">{{ pageTitle }}</span>
    </div>

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">{{ pageTitle }}</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">
        {{ isEditMode ? 'Update the announcement details below' : 'Create a new client-facing announcement' }}
      </p>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading announcement...
    </div>

    <!-- Error -->
    <div v-if="error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-4">
      {{ error }}
    </div>

    <!-- Form -->
    <div v-if="!loading" class="bg-surface-card border border-border rounded-2xl p-6 space-y-5">

      <!-- Date -->
      <div>
        <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date</label>
        <AppDatePicker v-model="form.date" placeholder="Select date" />
      </div>

      <!-- Title -->
      <div>
        <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Title</label>
        <input
          v-model="form.title"
          type="text"
          placeholder="Announcement title"
          class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
        />
      </div>

      <!-- Content -->
      <div>
        <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Announcement</label>
        <textarea
          v-model="form.content"
          placeholder="Announcement content..."
          class="w-full min-h-[300px] bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
        />
      </div>

      <!-- Published -->
      <div class="flex items-center gap-3">
        <AppCheckbox v-model="form.published" />
        <span class="text-[0.82rem] text-text-secondary cursor-pointer" @click="form.published = !form.published">Published</span>
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
          <span v-else>{{ isEditMode ? 'Update Announcement' : 'Save Announcement' }}</span>
        </button>
        <RouterLink
          to="/support/announcements"
          class="px-4 py-2 text-[0.82rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.06] transition-colors"
        >
          Cancel
        </RouterLink>
      </div>

    </div>

  </div>
</template>
