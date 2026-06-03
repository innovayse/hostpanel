<script setup lang="ts">
/**
 * Dropdown with checkboxes for selecting visible fields/columns.
 * Compact trigger button showing count of selected fields.
 */
import { ref, computed, onMounted, onUnmounted } from 'vue'
import AppCheckbox from './AppCheckbox.vue'

const props = defineProps<{
  /** All available fields. */
  fields: { key: string; label: string }[]
  /** Currently selected field keys. */
  selected: Set<string>
}>()

const emit = defineEmits<{
  toggle: [key: string]
  selectAll: []
  clearAll: []
}>()

const open = ref(false)
const root = ref<HTMLElement | null>(null)

const selectedCount = computed(() => props.selected.size)
const totalCount = computed(() => props.fields.length)

function onOutsideClick(e: MouseEvent) {
  if (root.value && !root.value.contains(e.target as Node)) open.value = false
}

onMounted(() => document.addEventListener('mousedown', onOutsideClick))
onUnmounted(() => document.removeEventListener('mousedown', onOutsideClick))
</script>

<template>
  <div ref="root" class="relative">
    <!-- Trigger -->
    <button
      type="button"
      class="w-full flex items-center gap-2 bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-secondary hover:text-text-primary hover:border-text-muted focus:outline-none focus:border-primary-500/50 transition-colors"
      @click="open = !open"
    >
      <svg class="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <rect x="3" y="3" width="7" height="7" /><rect x="14" y="3" width="7" height="7" /><rect x="3" y="14" width="7" height="7" /><rect x="14" y="14" width="7" height="7" />
      </svg>
      <span class="flex-1 text-left">Fields to Include</span>
      <span v-if="selectedCount > 0" class="text-[0.68rem] bg-primary-500/20 text-primary-400 px-1.5 py-0.5 rounded-full font-medium shrink-0">{{ selectedCount }}/{{ totalCount }}</span>
      <svg class="w-3.5 h-3.5 text-text-muted shrink-0 transition-transform duration-150 ml-auto" :class="open ? 'rotate-180' : ''" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9" /></svg>
    </button>

    <!-- Dropdown -->
    <Transition
      enter-active-class="transition duration-100 ease-out"
      enter-from-class="opacity-0 scale-95 -translate-y-1"
      enter-to-class="opacity-100 scale-100 translate-y-0"
      leave-active-class="transition duration-75 ease-in"
      leave-from-class="opacity-100 scale-100 translate-y-0"
      leave-to-class="opacity-0 scale-95 -translate-y-1"
    >
      <div v-if="open" class="absolute z-50 mt-1.5 left-0 bg-surface-card border border-border rounded-[10px] shadow-xl w-[420px] max-h-[320px] overflow-hidden">
        <!-- Select All / Clear All -->
        <div class="flex items-center justify-between px-3 py-2 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Fields</span>
          <div class="flex gap-2">
            <button type="button" class="text-[0.72rem] text-primary-400 hover:text-primary-300 transition-colors" @click="emit('selectAll')">Select All</button>
            <span class="text-text-muted">|</span>
            <button type="button" class="text-[0.72rem] text-text-muted hover:text-text-secondary transition-colors" @click="emit('clearAll')">Clear All</button>
          </div>
        </div>

        <!-- Checkboxes grid -->
        <div class="p-3 overflow-y-auto max-h-[260px]">
          <div class="grid grid-cols-2 gap-x-4 gap-y-1.5">
            <label
              v-for="field in fields"
              :key="field.key"
              class="flex items-center gap-1.5 text-[0.78rem] text-text-secondary cursor-pointer select-none hover:text-text-primary transition-colors py-0.5"
            >
              <AppCheckbox :modelValue="selected.has(field.key)" @update:modelValue="emit('toggle', field.key)" />
              {{ field.label }}
            </label>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>
