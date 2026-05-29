<script setup lang="ts">
<<<<<<< HEAD
import { computed, ref, onMounted, onUnmounted, watch } from 'vue'
import AppSelect from './AppSelect.vue'

const props = defineProps<{
  modelValue: string | null
  placeholder?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
}>()

const isOpen = ref(false)
const openAbove = ref(false)
const currentMonth = ref(new Date())
const popupRef = ref<HTMLElement | null>(null)
const inputRef = ref<HTMLInputElement | null>(null)
const inputText = ref('')
const previewDay = ref<number | null>(null)
const previewMonth = ref<number | null>(null)
const previewYear = ref<number | null>(null)

const monthOptions = computed(() =>
  monthNames.map((name, i) => ({ value: String(i), label: name }))
)

const syncInputText = () => {
  if (!props.modelValue) {
    inputText.value = ''
  } else {
    inputText.value = formatDisplayDate(props.modelValue)
  }
}

onMounted(() => {
  syncInputText()
})

watch(isOpen, (newValue) => {
  if (newValue) {
    // Use nextTick to ensure DOM is updated before measuring
    setTimeout(() => checkPopupPosition(), 0)
  }
})

const yearOptions = computed(() => {
  const years: { value: string; label: string }[] = []
  for (let y = 2020; y <= 2030; y++) {
    years.push({ value: String(y), label: String(y) })
  }
  return years
})

const currentMonthValue = computed({
  get: () => String(currentMonth.value.getMonth()),
  set: (value) => {
    const newMonth = parseInt(value)
    currentMonth.value = new Date(currentMonth.value.getFullYear(), newMonth, 1)
  },
})

const currentYearValue = computed({
  get: () => String(currentMonth.value.getFullYear()),
  set: (value) => {
    currentMonth.value = new Date(parseInt(value), currentMonth.value.getMonth(), 1)
  },
})

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
  if (props.modelValue) {
    const date = new Date(props.modelValue)
    currentMonth.value = new Date(date.getFullYear(), date.getMonth(), 1)
  }
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})

const handleClickOutside = (e: MouseEvent) => {
  if (popupRef.value && !popupRef.value.contains(e.target as Node)) {
    isOpen.value = false
  }
}

const checkPopupPosition = () => {
  if (!inputRef.value) return

  const inputRect = inputRef.value.getBoundingClientRect()
  const popupHeight = 450 // Approximate height of the popup
  const spaceBelow = window.innerHeight - inputRect.bottom
  const spaceAbove = inputRect.top

  openAbove.value = spaceBelow < popupHeight && spaceAbove > popupHeight
}

const formatDisplayDate = (dateStr: string) => {
  const [year, month, day] = dateStr.split('-')
  return `${day}/${month}/${year}`
}

const inputValue = ref('')

const displayText = computed(() => {
  if (!props.modelValue) return props.placeholder ?? 'Select date...'
  return formatDisplayDate(props.modelValue)
})

const parseDateInput = (input: string) => {
  // Try to parse DD/MM/YYYY or DDMMYYYY
  const cleaned = input.replace(/\D/g, '')
  if (cleaned.length === 8) {
    const day = cleaned.substring(0, 2)
    const month = cleaned.substring(2, 4)
    const year = cleaned.substring(4, 8)

    const dayNum = parseInt(day)
    const monthNum = parseInt(month)
    const yearNum = parseInt(year)

    if (dayNum >= 1 && dayNum <= 31 && monthNum >= 1 && monthNum <= 12 && yearNum >= 2000) {
      const dateStr = `${yearNum}-${String(monthNum).padStart(2, '0')}-${String(dayNum).padStart(2, '0')}`
      const testDate = new Date(dateStr)
      if (!isNaN(testDate.getTime())) {
        return dateStr
      }
    }
  }
  return null
}

const parsePartialDate = (input: string) => {
  const cleaned = input.replace(/\D/g, '')
  const result: { day?: number; month?: number; year?: number } = {}

  if (cleaned.length >= 1) {
    const day = cleaned.substring(0, 2)
    const dayNum = parseInt(day)
    if (dayNum >= 1 && dayNum <= 31) {
      result.day = dayNum
    }
  }

  if (cleaned.length >= 3) {
    const month = cleaned.substring(2, 4)
    const monthNum = parseInt(month)
    if (monthNum >= 1 && monthNum <= 12) {
      result.month = monthNum
    }
  }

  if (cleaned.length >= 4) {
    const year = cleaned.substring(4, 8)
    if (year.length >= 1) {
      const yearNum = parseInt(year)
      if (yearNum >= 2000 && yearNum <= 2099) {
        result.year = yearNum
      }
    }
  }

  return result
}

const handleInput = (event: Event) => {
  const target = event.target as HTMLInputElement
  const input = inputText.value

  // If input is empty, clear the date
  if (!input || input.trim() === '') {
    emit('update:modelValue', null)
    currentMonth.value = new Date()
    previewDay.value = null
    previewMonth.value = null
    previewYear.value = null
    return
  }

  // Try to parse as complete date first
  const parsed = parseDateInput(input)
  if (parsed) {
    emit('update:modelValue', parsed)
    const [year, month, day] = parsed.split('-')
    const yearNum = parseInt(year)
    const monthNum = parseInt(month)
    const dayNum = parseInt(day)
    currentMonth.value = new Date(yearNum, monthNum - 1, 1)
    // Clear preview when a complete date is confirmed
    previewDay.value = null
    previewMonth.value = null
    previewYear.value = null
  } else {
    // Try to parse partial date and update calendar in real-time
    const partial = parsePartialDate(input)

    if (partial.year || partial.month || partial.day) {
      const year = partial.year || currentMonth.value.getFullYear()
      const month = partial.month || (currentMonth.value.getMonth() + 1)

      currentMonth.value = new Date(year, month - 1, 1)

      // Update preview for highlighting
      previewDay.value = partial.day || null
      previewMonth.value = partial.month || null
      previewYear.value = partial.year || null
    }
  }
}

const daysInMonth = (date: Date) => {
  return new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate()
}

const firstDayOfMonth = (date: Date) => {
  return new Date(date.getFullYear(), date.getMonth(), 1).getDay()
}

const getDaysForMonth = (date: Date) => {
  const days: (number | null)[] = []
  const firstDay = firstDayOfMonth(date)

  for (let i = 0; i < firstDay; i++) {
    days.push(null)
  }

  const daysCount = daysInMonth(date)
  for (let i = 1; i <= daysCount; i++) {
    days.push(i)
  }

  return days
}

const isSelectedDate = (day: number, month: Date) => {
  // Check if this day matches the preview (while typing)
  if (previewDay.value !== null && previewMonth.value !== null && previewYear.value !== null) {
    return (
      day === previewDay.value &&
      month.getMonth() === previewMonth.value - 1 &&
      month.getFullYear() === previewYear.value
    )
  }

  // Check if this day matches the confirmed date
  if (!props.modelValue) return false
  const current = new Date(month.getFullYear(), month.getMonth(), day)
  const year = current.getFullYear()
  const monthStr = String(current.getMonth() + 1).padStart(2, '0')
  const dayStr = String(current.getDate()).padStart(2, '0')
  const currentDateString = `${year}-${monthStr}-${dayStr}`
  return currentDateString === props.modelValue
}

const formatDateString = (date: Date) => {
  const year = date.getFullYear()
  const monthStr = String(date.getMonth() + 1).padStart(2, '0')
  const dayStr = String(date.getDate()).padStart(2, '0')
  return `${year}-${monthStr}-${dayStr}`
}

const getSelectedQuickDate = () => {
  if (!props.modelValue) return null
  const selectedDate = props.modelValue

  const today = new Date()
  if (selectedDate === formatDateString(today)) {
    return 'today'
  }

  const yesterday = new Date(today)
  yesterday.setDate(yesterday.getDate() - 1)
  if (selectedDate === formatDateString(yesterday)) {
    return 'yesterday'
  }

  const sevenDaysAgo = new Date(today)
  sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7)
  if (selectedDate === formatDateString(sevenDaysAgo)) {
    return '7days'
  }

  const oneMonthAgo = new Date(today)
  oneMonthAgo.setMonth(oneMonthAgo.getMonth() - 1)
  if (selectedDate === formatDateString(oneMonthAgo)) {
    return '1month'
  }

  const oneYearAgo = new Date(today)
  oneYearAgo.setFullYear(oneYearAgo.getFullYear() - 1)
  if (selectedDate === formatDateString(oneYearAgo)) {
    return '1year'
  }

  return 'custom'
}

const selectDate = (day: number, month: Date) => {
  const date = new Date(month.getFullYear(), month.getMonth(), day)
  const year = date.getFullYear()
  const monthStr = String(date.getMonth() + 1).padStart(2, '0')
  const dayStr = String(date.getDate()).padStart(2, '0')
  const dateString = `${year}-${monthStr}-${dayStr}`
  emit('update:modelValue', dateString)
  inputText.value = formatDisplayDate(dateString)
  previewDay.value = null
  previewMonth.value = null
  previewYear.value = null
  isOpen.value = false
}

const selectQuickDate = (offset: number | string) => {
  const today = new Date()
  let date: Date

  if (typeof offset === 'string') {
    if (offset === 'today') {
      date = new Date(today)
    } else if (offset === 'yesterday') {
      date = new Date(today)
      date.setDate(date.getDate() - 1)
    } else if (offset === '7days') {
      date = new Date(today)
      date.setDate(date.getDate() - 7)
    } else if (offset === '1month') {
      date = new Date(today)
      date.setMonth(date.getMonth() - 1)
    } else if (offset === '1year') {
      date = new Date(today)
      date.setFullYear(date.getFullYear() - 1)
    }
  } else {
    date = new Date(today)
    date.setDate(date.getDate() - offset)
  }

  const year = date!.getFullYear()
  const monthStr = String(date!.getMonth() + 1).padStart(2, '0')
  const dayStr = String(date!.getDate()).padStart(2, '0')
  const dateString = `${year}-${monthStr}-${dayStr}`
  emit('update:modelValue', dateString)
  inputText.value = formatDisplayDate(dateString)
  previewDay.value = null
  previewMonth.value = null
  previewYear.value = null
  currentMonth.value = new Date(date!.getFullYear(), date!.getMonth(), 1)
  isOpen.value = false
}

const prevMonth = () => {
  currentMonth.value = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth() - 1)
}

const nextMonth = () => {
  currentMonth.value = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth() + 1)
}

const monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
const dayNames = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa']
</script>

<template>
  <div class="relative" ref="popupRef">
    <!-- Input field -->
    <input
      ref="inputRef"
      v-model="inputText"
      type="text"
      placeholder="DD/MM/YYYY"
      @click="isOpen = true"
      @input="handleInput"
      @focus="isOpen = true"
      class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] focus:outline-none focus:border-primary-500/40 transition-colors"
    />

    <!-- Popup -->
    <div
      v-if="isOpen"
      :class="openAbove ? 'bottom-full mb-2' : 'top-full mt-2'"
      class="absolute left-0 z-50 bg-surface-card rounded-[10px] shadow-2xl border border-border overflow-hidden"
    >
      <div class="flex">
        <!-- Quick Selection Panel -->
        <div class="w-36 bg-white/[0.02] border-r border-border p-3 flex flex-col gap-1">
          <button
            type="button"
            @click="selectQuickDate('today')"
            :class="getSelectedQuickDate() === 'today' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            Today
          </button>
          <button
            type="button"
            @click="selectQuickDate('yesterday')"
            :class="getSelectedQuickDate() === 'yesterday' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            Yesterday
          </button>
          <button
            type="button"
            @click="selectQuickDate('7days')"
            :class="getSelectedQuickDate() === '7days' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            7 Days Ago
          </button>
          <button
            type="button"
            @click="selectQuickDate('1month')"
            :class="getSelectedQuickDate() === '1month' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            1 Month Ago
          </button>
          <button
            type="button"
            @click="selectQuickDate('1year')"
            :class="getSelectedQuickDate() === '1year' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            1 Year Ago
          </button>
          <button
            type="button"
            @click="isOpen = true"
            :class="getSelectedQuickDate() === 'custom' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            Custom
          </button>
        </div>

        <!-- Calendar Panel -->
        <div class="p-4 w-80">
          <!-- Navigation -->
          <div class="flex items-center justify-between mb-4">
            <button @click="prevMonth" class="text-text-muted hover:text-text-primary text-lg transition-colors">‹</button>
            <div class="flex items-center gap-2">
              <div class="w-[140px]">
                <AppSelect
                  v-model="currentMonthValue"
                  :options="monthOptions"
                />
              </div>
              <div class="w-[100px]">
                <AppSelect
                  v-model="currentYearValue"
                  :options="yearOptions"
                />
              </div>
            </div>
            <button @click="nextMonth" class="text-text-muted hover:text-text-primary text-lg transition-colors">›</button>
          </div>

          <!-- Calendar -->
          <div>
            <!-- Days of week -->
            <div class="grid grid-cols-7 gap-1 mb-2">
              <div v-for="day in dayNames" :key="day" class="text-center text-xs font-semibold text-text-muted">{{ day }}</div>
            </div>
            <!-- Days -->
            <div class="grid grid-cols-7 gap-1">
              <button
                type="button"
                v-for="(day, i) in getDaysForMonth(currentMonth)"
                :key="i"
                @click="day && selectDate(day as number, currentMonth)"
                :disabled="!day"
                :class="{
                  'bg-primary-500 text-white rounded font-semibold': day && isSelectedDate(day as number, currentMonth),
                  'text-text-muted cursor-not-allowed': !day,
                  'text-text-primary hover:bg-white/[0.05]': day && !isSelectedDate(day as number, currentMonth),
                }"
                class="h-8 flex items-center justify-center text-sm rounded transition-colors"
              >
                {{ day }}
              </button>
            </div>
          </div>

          <!-- Bottom buttons -->
          <div class="flex items-center justify-between border-t border-border mt-4 pt-4">
            <button type="button" @click="() => { emit('update:modelValue', null); isOpen = false }" class="text-sm text-text-secondary hover:text-text-primary transition-colors">Clear</button>
            <button type="button" @click="isOpen = false" class="text-sm text-text-secondary hover:text-text-primary transition-colors">Close</button>
          </div>
        </div>
      </div>
    </div>
=======
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
>>>>>>> origin/main
  </div>
</template>
