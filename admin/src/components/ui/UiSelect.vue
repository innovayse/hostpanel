<script setup lang="ts" generic="T extends string | number | null">
/**
 * Themed dropdown select with an optional search filter.
 *
 * Replaces the native `<select>`, whose options cannot be themed across browsers. The trigger and
 * the option list read theme tokens, so the control follows light/dark. `data-testid` and classes
 * land on the root element.
 */
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import { fieldInvalidClasses } from '@/utils/ui/input'

const { t } = useI18n()

/** Props for UiSelect. */
const props = defineProps<{
  /** Currently selected value. */
  modelValue: T
  /** Options to display. Each option may include a displayLabel for the dropdown. */
  options: { value: T; label: string; displayLabel?: string }[]
  /** Text shown in the trigger while nothing is selected. */
  placeholder?: string
  /** Disables the select. */
  disabled?: boolean
  /** Enables a search input inside the dropdown. */
  searchable?: boolean
  /** Gives the dropdown panel a wider minimum width, for long labels (country and phone lists). */
  wide?: boolean
  /** Id of the trigger button, so a caption can point at it (`UiField` hands one out). */
  id?: string
  /** Marks the value as refused by the server (red border, `aria-invalid`). */
  invalid?: boolean
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

/** The label of the currently selected option (short form for the trigger). */
const selectedLabel = computed<string>(
  () => props.options.find(o => o.value === props.modelValue)?.label ?? props.placeholder ?? '—',
)

/**
 * Whether the trigger is showing the placeholder instead of a chosen label.
 *
 * Decided by whether an option matches the value, never by comparing texts: a select whose
 * placeholder equals the chosen option's label (the language switch shows "English" for both)
 * would otherwise read as empty and be drawn dimmed, like a disabled control.
 */
const showingPlaceholder = computed<boolean>(() => !props.options.some(o => o.value === props.modelValue))

/** Options after the search term is applied (matches both label and displayLabel). */
const filteredOptions = computed(() => {
  if (!props.searchable || !search.value) {
    return props.options
  }
  const term = search.value.toLowerCase()
  return props.options.filter(o =>
    o.label.toLowerCase().includes(term)
    || (o.displayLabel?.toLowerCase().includes(term)),
  )
})

/**
 * Selects an option and closes the dropdown.
 *
 * @param value - The value to select.
 */
const select = (value: T): void => {
  emit('update:modelValue', value)
  open.value = false
  search.value = ''
}

/** Toggles the dropdown and focuses the search input when there is one. */
const toggle = (): void => {
  open.value = !open.value
  if (open.value && props.searchable) {
    void nextTick(() => searchInput.value?.focus())
  }
}

/**
 * Closes the dropdown when the pointer goes down outside the component.
 *
 * @param e - The mouse event.
 */
const onOutsideClick = (e: MouseEvent): void => {
  if (root.value && !root.value.contains(e.target as Node)) {
    open.value = false
    search.value = ''
  }
}

watch(open, (val) => {
  if (!val) {
    search.value = ''
  }
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
      :aria-expanded="open"
      aria-haspopup="listbox"
      data-testid="ui-select-trigger"
      :id="props.id"
      :aria-invalid="props.invalid || undefined"
      class="w-full flex items-center justify-between gap-2 bg-surface-field border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary outline-none focus-visible:border-primary-500/50 focus-visible:ring-2 focus-visible:ring-primary-500/30 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      :class="[open ? 'border-primary-500/50 ring-2 ring-primary-500/20' : '', props.invalid ? fieldInvalidClasses : '']"
      @click="toggle"
    >
      <span class="truncate" :class="showingPlaceholder ? 'text-text-muted' : 'text-text-primary'">
        {{ selectedLabel }}
      </span>
      <svg
        class="w-4 h-4 text-text-secondary shrink-0 transition-transform duration-150"
        :class="open ? 'rotate-180' : ''"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        aria-hidden="true"
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
        role="listbox"
        class="absolute z-50 mt-1.5 bg-surface-card border border-border rounded-[10px] shadow-xl overflow-hidden"
        :class="wide ? 'min-w-[18rem]' : 'w-full'"
      >
        <!-- Search input -->
        <div v-if="props.searchable" class="p-1.5 border-b border-border">
          <input
            ref="searchInput"
            v-model="search"
            type="text"
            :placeholder="t('shared.search')"
            class="w-full bg-surface-field border border-border rounded-lg px-2.5 py-1.5 text-[0.82rem] text-text-primary placeholder:text-text-muted outline-none focus:border-primary-500/50 focus:ring-2 focus:ring-primary-500/20 transition-colors"
            @click.stop
          >
        </div>

        <div class="max-h-56 overflow-y-auto py-1">
          <button
            v-for="opt in filteredOptions"
            :key="String(opt.value)"
            type="button"
            role="option"
            data-testid="ui-select-option"
            :aria-selected="opt.value === props.modelValue"
            class="w-full flex items-center justify-between px-3 py-1.5 text-[0.82rem] transition-colors outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500/40"
            :class="opt.value === props.modelValue
              ? 'bg-primary-600 light:bg-primary-700 text-white font-medium'
              : 'text-text-secondary hover:text-text-primary hover:bg-surface-hover focus-visible:text-text-primary focus-visible:bg-surface-hover'"
            @mousedown.prevent.stop="select(opt.value)"
          >
            <span class="truncate">{{ opt.displayLabel ?? opt.label }}</span>
            <svg
              v-if="opt.value === props.modelValue"
              class="w-3.5 h-3.5 text-white shrink-0 ml-2"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2.5"
              aria-hidden="true"
            >
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </button>
          <p v-if="filteredOptions.length === 0" class="px-3 py-2 text-[0.82rem] text-text-secondary">
            {{ t('ui.uiSelect.noResults') }}
          </p>
        </div>
      </div>
    </Transition>
  </div>
</template>
