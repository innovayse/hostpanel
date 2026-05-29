<script setup lang="ts">
/**
 * Custom dark-themed date picker matching the admin panel design.
 * Replaces native <input type="date"> whose calendar popup cannot be styled.
 * Uses the same visual language as AppSelect (surfaces, borders, transitions).
 */
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'

const props = withDefaults(defineProps<{
  /** Currently selected date in YYYY-MM-DD format. */
  modelValue: string
  /** Placeholder text when no date is selected. */
  placeholder?: string
  /** Disables the picker. */
  disabled?: boolean
}>(), {
  placeholder: 'Select date',
  disabled: false,
})

const emit = defineEmits<{
  /** Emitted when the user picks a date. */
  'update:modelValue': [value: string]
}>()

/** Whether the calendar dropdown is open. */
const open = ref(false)

/** Root element ref for outside-click detection. */
const root = ref<HTMLElement | null>(null)

/** Currently viewed month (0-11). */
const viewMonth = ref(0)

/** Currently viewed year. */
const viewYear = ref(2026)

/** Day names header. */
const dayNames = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su']

/** Month names for the header. */
const monthNames = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

/** Formatted display value. */
const displayValue = computed(() => {
  if (!props.modelValue) return ''
  const d = new Date(props.modelValue + 'T00:00:00')
  if (isNaN(d.getTime())) return props.modelValue
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
})

/** The selected date as YYYY-MM-DD for comparison. */
const selectedDate = computed(() => props.modelValue || '')

/** Today's date as YYYY-MM-DD. */
const today = computed(() => {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
})

/** Calendar grid: array of week arrays, each containing day objects. */
const calendarDays = computed(() => {
  const year = viewYear.value
  const month = viewMonth.value
  const firstDay = new Date(year, month, 1)
  const lastDay = new Date(year, month + 1, 0)

  // Monday=0 based day of week for first day
  let startDow = firstDay.getDay() - 1
  if (startDow < 0) startDow = 6

  const days: Array<{ date: string; day: number; currentMonth: boolean }> = []

  // Previous month fill
  for (let i = startDow - 1; i >= 0; i--) {
    const d = new Date(year, month, -i)
    days.push({
      date: formatYmd(d),
      day: d.getDate(),
      currentMonth: false,
    })
  }

  // Current month
  for (let i = 1; i <= lastDay.getDate(); i++) {
    const d = new Date(year, month, i)
    days.push({
      date: formatYmd(d),
      day: i,
      currentMonth: true,
    })
  }

  // Next month fill to complete grid (always 42 cells = 6 rows)
  const remaining = 42 - days.length
  for (let i = 1; i <= remaining; i++) {
    const d = new Date(year, month + 1, i)
    days.push({
      date: formatYmd(d),
      day: i,
      currentMonth: false,
    })
  }

  // Chunk into weeks
  const weeks: typeof days[] = []
  for (let i = 0; i < days.length; i += 7) {
    weeks.push(days.slice(i, i + 7))
  }
  return weeks
})

/**
 * Formats a Date to YYYY-MM-DD.
 *
 * @param d - The date to format.
 * @returns Formatted string.
 */
function formatYmd(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/**
 * Selects a date and closes the calendar.
 *
 * @param date - YYYY-MM-DD string.
 */
function selectDate(date: string): void {
  emit('update:modelValue', date)
  open.value = false
}

/** Navigates to the previous month. */
function prevMonth(): void {
  if (viewMonth.value === 0) {
    viewMonth.value = 11
    viewYear.value--
  } else {
    viewMonth.value--
  }
}

/** Navigates to the next month. */
function nextMonth(): void {
  if (viewMonth.value === 11) {
    viewMonth.value = 0
    viewYear.value++
  } else {
    viewMonth.value++
  }
}

/** Toggles the calendar dropdown. */
function toggle(): void {
  if (props.disabled) return
  open.value = !open.value
}

/**
 * Closes the dropdown when clicking outside.
 *
 * @param e - The mouse event.
 */
function onOutsideClick(e: MouseEvent): void {
  if (root.value && !root.value.contains(e.target as Node)) {
    open.value = false
  }
}

// Sync view to selected date when opening
watch(open, (val) => {
  if (val && props.modelValue) {
    const d = new Date(props.modelValue + 'T00:00:00')
    if (!isNaN(d.getTime())) {
      viewMonth.value = d.getMonth()
      viewYear.value = d.getFullYear()
    }
  } else if (val) {
    const now = new Date()
    viewMonth.value = now.getMonth()
    viewYear.value = now.getFullYear()
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
      :disabled="disabled"
      class="w-full flex items-center justify-between gap-2 bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      :class="open ? 'border-primary-500/50 ring-1 ring-primary-500/10' : ''"
      @click="toggle"
    >
      <span class="truncate" :class="displayValue ? 'text-text-primary' : 'text-text-muted'">
        {{ displayValue || placeholder }}
      </span>
      <svg class="w-4 h-4 text-text-muted shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
        <rect x="3" y="4" width="18" height="18" rx="2" />
        <line x1="16" y1="2" x2="16" y2="6" />
        <line x1="8" y1="2" x2="8" y2="6" />
        <line x1="3" y1="10" x2="21" y2="10" />
      </svg>
    </button>

    <!-- Calendar dropdown -->
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
        class="absolute z-50 mt-1.5 bg-surface-card border border-border rounded-[10px] shadow-xl p-3 w-[280px]"
      >
        <!-- Month/Year navigation -->
        <div class="flex items-center justify-between mb-2">
          <button
            type="button"
            class="p-1.5 rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.05] transition-colors"
            @mousedown.prevent.stop="prevMonth"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="15 18 9 12 15 6" />
            </svg>
          </button>
          <span class="text-[0.82rem] font-medium text-text-primary">
            {{ monthNames[viewMonth] }} {{ viewYear }}
          </span>
          <button
            type="button"
            class="p-1.5 rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.05] transition-colors"
            @mousedown.prevent.stop="nextMonth"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="9 6 15 12 9 18" />
            </svg>
          </button>
        </div>

        <!-- Day headers -->
        <div class="grid grid-cols-7 gap-0 mb-1">
          <span
            v-for="day in dayNames"
            :key="day"
            class="text-center text-[0.68rem] font-semibold uppercase tracking-wide text-text-muted py-1"
          >
            {{ day }}
          </span>
        </div>

        <!-- Calendar grid -->
        <div v-for="(week, wi) in calendarDays" :key="wi" class="grid grid-cols-7 gap-0">
          <button
            v-for="d in week"
            :key="d.date"
            type="button"
            class="w-full aspect-square flex items-center justify-center text-[0.78rem] rounded-lg transition-colors"
            :class="[
              d.date === selectedDate
                ? 'bg-primary-500 text-white font-semibold'
                : d.date === today
                  ? 'text-primary-400 font-medium hover:bg-white/[0.05]'
                  : d.currentMonth
                    ? 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'
                    : 'text-text-muted/40 hover:bg-white/[0.03]',
            ]"
            @mousedown.prevent.stop="selectDate(d.date)"
          >
            {{ d.day }}
          </button>
        </div>

        <!-- Today shortcut -->
        <div class="mt-2 pt-2 border-t border-border flex justify-center">
          <button
            type="button"
            class="text-[0.78rem] text-primary-400 hover:text-primary-300 transition-colors"
            @mousedown.prevent.stop="selectDate(today)"
          >
            Today
          </button>
        </div>
      </div>
    </Transition>
  </div>
</template>
