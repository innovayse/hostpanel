<script setup lang="ts">
/**
 * Modal dialog for creating or editing a server group.
 *
 * Displays a dual-list selector for assigning servers.
 * Emits `saved` with the payload on submit, and `close` on cancel.
 */
import { ref, watch } from 'vue'
import type { ServerDto, ServerGroupDto, ServerGroupPayload, ServerFillType } from '../types/server.types'
import { FILL_TYPE_LABELS } from '../types/server.types'

/** Props for GroupFormModal. */
const props = defineProps<{
  /** Group to edit, or null when creating. */
  group: ServerGroupDto | null
  /** All available servers to choose from. */
  availableServers: ServerDto[]
  /** True while the save request is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the user submits a valid form. */
  saved: [payload: ServerGroupPayload]
  /** Emitted when the user closes/cancels the modal. */
  close: []
}>()

const fillTypes: ServerFillType[] = ['LeastFull', 'FillUntilFull']

const name = ref('')
const fillType = ref<ServerFillType>('LeastFull')
const selectedIds = ref<number[]>([])

watch(() => props.group, (g) => {
  if (g) {
    name.value = g.name
    fillType.value = g.fillType
    selectedIds.value = g.servers.map(s => s.id)
  } else {
    name.value = ''
    fillType.value = 'LeastFull'
    selectedIds.value = []
  }
}, { immediate: true })

/** Servers NOT yet in the group. */
const unselected = () => props.availableServers.filter(s => !selectedIds.value.includes(s.id))

/** Servers in the group. */
const selected = () => props.availableServers.filter(s => selectedIds.value.includes(s.id))

/** Toggles a server in/out of the group. */
function toggle(id: number): void {
  if (selectedIds.value.includes(id))
    selectedIds.value = selectedIds.value.filter(x => x !== id)
  else
    selectedIds.value = [...selectedIds.value, id]
}

/** Submits the form. */
function handleSubmit(): void {
  emit('saved', { name: name.value, fillType: fillType.value, serverIds: selectedIds.value })
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')" />

    <div class="relative bg-surface-card border border-border rounded-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto shadow-2xl">

      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border">
        <h2 class="font-display font-bold text-[1rem] text-text-primary">
          {{ props.group ? 'Edit Group' : 'Create New Group' }}
        </h2>
        <button
          class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
          @click="emit('close')"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
          </svg>
        </button>
      </div>

      <form class="px-6 py-5 flex flex-col gap-4" @submit.prevent="handleSubmit">

        <!-- Name -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Group Name</label>
          <input
            v-model="name"
            required
            type="text"
            placeholder="e.g. CWP Servers"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Fill type -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Fill Type</label>
          <div class="flex flex-col gap-2">
            <label
              v-for="ft in fillTypes"
              :key="ft"
              class="flex items-center gap-2.5 cursor-pointer"
            >
              <div
                class="w-4 h-4 rounded-full border-2 flex items-center justify-center shrink-0 transition-colors"
                :class="fillType === ft ? 'border-primary-500' : 'border-border'"
                @click="fillType = ft"
              >
                <div v-if="fillType === ft" class="w-1.5 h-1.5 rounded-full bg-primary-500" />
              </div>
              <span class="text-[0.82rem] text-text-secondary">{{ FILL_TYPE_LABELS[ft] }}</span>
            </label>
          </div>
        </div>

        <!-- Server selector -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Servers</label>

          <div class="grid grid-cols-2 gap-2">
            <!-- Available -->
            <div>
              <p class="text-[0.68rem] text-text-muted mb-1">Available</p>
              <div class="bg-white/[0.03] border border-border rounded-xl min-h-[120px] max-h-[160px] overflow-y-auto divide-y divide-border">
                <button
                  v-for="s in unselected()"
                  :key="s.id"
                  type="button"
                  class="w-full text-left px-3 py-2 text-[0.78rem] text-text-secondary hover:bg-white/[0.04] hover:text-text-primary transition-colors"
                  @click="toggle(s.id)"
                >
                  {{ s.name }}
                  <span class="text-primary-400 ml-1">+</span>
                </button>
                <p v-if="unselected().length === 0" class="px-3 py-2 text-[0.74rem] text-text-muted italic">All assigned</p>
              </div>
            </div>

            <!-- Selected -->
            <div>
              <p class="text-[0.68rem] text-text-muted mb-1">In Group</p>
              <div class="bg-white/[0.03] border border-border rounded-xl min-h-[120px] max-h-[160px] overflow-y-auto divide-y divide-border">
                <button
                  v-for="s in selected()"
                  :key="s.id"
                  type="button"
                  class="w-full text-left px-3 py-2 text-[0.78rem] text-text-secondary hover:bg-status-red/[0.06] hover:text-status-red transition-colors"
                  @click="toggle(s.id)"
                >
                  {{ s.name }}
                  <span class="text-status-red ml-1">×</span>
                </button>
                <p v-if="selected().length === 0" class="px-3 py-2 text-[0.74rem] text-text-muted italic">None selected</p>
              </div>
            </div>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex items-center justify-end gap-2.5 pt-1">
          <button
            type="button"
            class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
            @click="emit('close')"
          >
            Cancel
          </button>
          <button
            type="submit"
            :disabled="props.saving"
            class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          >
            {{ props.saving ? 'Saving…' : props.group ? 'Save Changes' : 'Create Group' }}
          </button>
        </div>

      </form>
    </div>
  </div>
</template>
