<script setup lang="ts" generic="T extends string | number">
/**
 * Custom styled dropdown select that matches the dark panel theme.
 * Replaces native <select> whose options cannot be themed cross-browser.
 * Supports an optional search filter for long option lists.
 */

import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'

/** Props for AppSelect. */
const props = defineProps<{
  /** Currently selected value. */
  modelValue: T
  /** Options to display. Each option may include a displayLabel for the dropdown. */
  options: { value: T; label: string; displayLabel?: string }[]
  /** Placeholder shown when nothing is selected. */
  placeholder?: string
  /** Disables the select. */
  disabled?: boolean
  /** Enables a search input inside the dropdown. */
  searchable?: boolean
  /** Minimum width for the dropdown panel (e.g. "16rem"). */
  dropdownWidth?: string
}>()

const emit = defineEmits<{
  /** Emitted when the user picks an option. */
  'update:modelValue': [value: T]
}>()

/** Whether the dropdown panel is open. */
const open = ref(false)

/** The root element ref, used for outside-click detection. */
const root = ref<HTMLElement | null>(null)

/** Search input ref for auto-focus. */
const searchInput = ref<HTMLInputElement | null>(null)

/** Search term for filtering options. */
const search = ref('')

/** The label of the currently selected option (short form for trigger). */
const selectedLabel = computed(
  () => props.options.find(o => o.value === props.modelValue)?.label ?? props.placeholder ?? '—'
)

/** Filtered options based on search term. Searches both label and displayLabel. */
const filteredOptions = computed(() => {
  if (!props.searchable || !search.value) return props.options
  const term = search.value.toLowerCase()
  return props.options.filter(o =>
    o.label.toLowerCase().includes(term) ||
    (o.displayLabel?.toLowerCase().includes(term))
  )
})

/**
 * Selects an option and closes the dropdown.
 *
 * @param value - The value to select.
 */
function select(value: T): void {
  emit('update:modelValue', value)
  open.value = false
  search.value = ''
}

/**
 * Toggles the dropdown and auto-focuses the search input.
 */
function toggle(): void {
  open.value = !open.value
  if (open.value && props.searchable) {
    nextTick(() => searchInput.value?.focus())
  }
}

/**
 * Closes the dropdown when clicking outside the component.
 *
 * @param e - The mouse event.
 */
function onOutsideClick(e: MouseEvent): void {
  if (root.value && !root.value.contains(e.target as Node)) {
    open.value = false
    search.value = ''
  }
}

watch(open, (val) => {
  if (!val) search.value = ''
})

onMounted(() => document.addEventListener('mousedown', onOutsideClick))
onUnmounted(() => document.removeEventListener('mousedown', onOutsideClick))
</script>

<template>
  <div ref="root" class="relative">
    <!-- Trigger -->
    <button
      type="button"
      :disabled="props.disabled"
      class="w-full flex items-center justify-between gap-2 bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      :class="open ? 'border-primary-500/50 ring-1 ring-primary-500/10' : ''"
      @click="toggle"
    >
      <span class="truncate" :class="selectedLabel === (props.placeholder ?? '—') ? 'text-text-muted' : 'text-text-primary'">
        {{ selectedLabel }}
      </span>
      <svg
        class="w-4 h-4 text-text-muted shrink-0 transition-transform duration-150"
        :class="open ? 'rotate-180' : ''"
        viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
      >
        <polyline points="6 9 12 15 18 9" />
      </svg>
    </button>

    <!-- Dropdown panel -->
    <Transition
      enter-active-class="transition duration-100 ease-out"
      enter-from-class="opacity-0 scale-95 -translate-y-1"
      enter-to-class="opacity-100 scale-100 translate-y-0"
      leave-active-class="transition duration-75 ease-in"
      leave-from-class="opacity-100 scale-100 translate-y-0"
      leave-to-class="opacity-0 scale-95 -translate-y-1"
    >
      <div
        v-if="open"
        class="absolute z-50 mt-1.5 bg-surface-card border border-border rounded-[10px] shadow-xl overflow-hidden"
        :class="dropdownWidth ? '' : 'w-full'"
        :style="dropdownWidth ? { minWidth: dropdownWidth } : undefined"
      >
        <!-- Search input -->
        <div v-if="props.searchable" class="p-1.5 border-b border-border">
          <input
            ref="searchInput"
            v-model="search"
            type="text"
            placeholder="Search..."
            class="w-full bg-white/[0.04] border border-border rounded-lg px-2.5 py-1.5 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 transition-colors"
            @click.stop
          />
        </div>

        <div class="max-h-56 overflow-y-auto py-1">
          <button
            v-for="opt in filteredOptions"
            :key="String(opt.value)"
            type="button"
            class="w-full flex items-center justify-between px-3 py-1.5 text-[0.82rem] transition-colors"
            :class="opt.value === props.modelValue
              ? 'text-primary-400 bg-primary-500/10'
              : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            @mousedown.prevent.stop="select(opt.value)"
          >
            <span class="truncate">{{ opt.displayLabel ?? opt.label }}</span>
            <svg
              v-if="opt.value === props.modelValue"
              class="w-3.5 h-3.5 text-primary-400 shrink-0 ml-2"
              viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"
            >
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </button>
          <p v-if="filteredOptions.length === 0" class="px-3 py-2 text-[0.82rem] text-text-muted">No results</p>
        </div>
      </div>
    </Transition>
  </div>
</template>
