<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import AppSelect from './AppSelect.vue'

const props = defineProps<{
  modelValue: [string, string] | null
  placeholder?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [[string, string] | null]
}>()

const isOpen = ref(false)
const selectedStart = ref<Date | null>(null)
const selectedEnd = ref<Date | null>(null)
const currentMonth = ref(new Date())

const popupRef = ref<HTMLElement | null>(null)
const inputRef = ref<HTMLInputElement | null>(null)
const wrapperRef = ref<HTMLElement | null>(null)

const popupStyle = ref({ top: '0px', left: '0px' })

function updatePopupPosition() {
  if (!wrapperRef.value) return
  const rect = wrapperRef.value.getBoundingClientRect()
  const popupWidth = Math.min(900, window.innerWidth - 32)
  let left = rect.left
  if (left + popupWidth > window.innerWidth - 16) {
    left = window.innerWidth - popupWidth - 16
  }
  if (left < 8) left = 8
  popupStyle.value = {
    top: `${rect.bottom + window.scrollY + 8}px`,
    left: `${left}px`,
  }
}
const inputText = ref('')
const previewStart = ref<Date | null>(null)
const previewEnd = ref<Date | null>(null)

const syncInputText = () => {
  if (!props.modelValue) {
    inputText.value = ''
  } else {
    inputText.value = `${formatDisplayDate(props.modelValue[0])} - ${formatDisplayDate(props.modelValue[1])}`
  }
}

onMounted(() => {
  syncInputText()
})

const monthOptions = computed(() =>
  monthNames.map((name, i) => ({ value: String(i), label: name }))
)

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

const nextMonthValue = computed({
  get: () => {
    const m2 = months.value[1]
    return String(m2?.getMonth() ?? 0)
  },
  set: (value) => {
    const newMonth = parseInt(value)
    const nextMonth = new Date(currentMonth.value)
    nextMonth.setMonth(newMonth)
    const prevMonth = newMonth === 0 ? 11 : newMonth - 1
    const prevYear = newMonth === 0 ? nextMonth.getFullYear() - 1 : nextMonth.getFullYear()
    currentMonth.value = new Date(prevYear, prevMonth, 1)
  },
})

const nextYearValue = computed({
  get: () => {
    const m2 = months.value[1]
    return String(m2?.getFullYear() ?? 2026)
  },
  set: (value) => {
    const newYear = parseInt(value)
    const nextDate = new Date(currentMonth.value)
    nextDate.setFullYear(newYear)
    nextDate.setMonth(nextDate.getMonth() - 1)
    currentMonth.value = nextDate
  },
})

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})

const handleClickOutside = (e: MouseEvent) => {
  if (
    popupRef.value && !popupRef.value.contains(e.target as Node) &&
    wrapperRef.value && !wrapperRef.value.contains(e.target as Node)
  ) {
    isOpen.value = false
  }
}

const displayText = computed(() => {
  if (!props.modelValue) return props.placeholder ?? 'Select date range...'
  return `${formatDisplayDate(props.modelValue[0])} - ${formatDisplayDate(props.modelValue[1])}`
})

const months = computed(() => {
  const m1 = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth())
  const m2 = new Date(m1.getFullYear(), m1.getMonth() + 1)
  return [m1, m2]
})

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

const isBetweenDates = (day: number, month: Date) => {
  const current = new Date(month.getFullYear(), month.getMonth(), day)

  // Check preview first (while typing)
  if (previewStart.value && previewEnd.value) {
    return current > previewStart.value && current < previewEnd.value
  }

  // Check confirmed dates
  if (!selectedStart.value || !selectedEnd.value) return false
  return current > selectedStart.value && current < selectedEnd.value
}

const isSelectedDate = (day: number, month: Date) => {
  const current = new Date(month.getFullYear(), month.getMonth(), day)

  // Check preview first (while typing)
  if (previewStart.value && current.getTime() === previewStart.value.getTime()) return 'start'
  if (previewEnd.value && current.getTime() === previewEnd.value.getTime()) return 'end'

  // Check confirmed dates
  const start = selectedStart.value ? new Date(selectedStart.value) : null
  const end = selectedEnd.value ? new Date(selectedEnd.value) : null

  if (start && current.getTime() === start.getTime()) return 'start'
  if (end && current.getTime() === end.getTime()) return 'end'
  return false
}

const selectDate = (day: number, month: Date) => {
  const date = new Date(month.getFullYear(), month.getMonth(), day)

  if (!selectedStart.value) {
    selectedStart.value = date
  } else if (!selectedEnd.value) {
    if (date < selectedStart.value) {
      selectedEnd.value = selectedStart.value
      selectedStart.value = date
    } else {
      selectedEnd.value = date
    }
  } else {
    selectedStart.value = date
    selectedEnd.value = null
  }
}

const applyRange = () => {
  if (selectedStart.value && selectedEnd.value) {
    const start = formatDateString(selectedStart.value)
    const end = formatDateString(selectedEnd.value)
    emit('update:modelValue', [start, end])
    inputText.value = `${formatDisplayDate(start)} - ${formatDisplayDate(end)}`
    isOpen.value = false
  }
}

const clearRange = () => {
  selectedStart.value = null
  selectedEnd.value = null
  inputText.value = ''
  emit('update:modelValue', null)
}

const formatDateString = (date: Date) => {
  const year = date.getFullYear()
  const monthStr = String(date.getMonth() + 1).padStart(2, '0')
  const dayStr = String(date.getDate()).padStart(2, '0')
  return `${year}-${monthStr}-${dayStr}`
}

const formatDisplayDate = (dateStr: string) => {
  const [year, month, day] = dateStr.split('-')
  return `${day}/${month}/${year}`
}

const parseSingleDate = (input: string) => {
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
  const input = inputText.value

  // If input is empty, clear the range
  if (!input || input.trim() === '') {
    emit('update:modelValue', null)
    selectedStart.value = null
    selectedEnd.value = null
    previewStart.value = null
    previewEnd.value = null
    currentMonth.value = new Date()
    return
  }

  // Try to parse as range (e.g., "25052026-31052026" or "25/05/2026 - 31/05/2026")
  const parts = input.split(/[\s\-]+/).filter(p => p.length > 0)

  if (parts.length >= 2) {
    const start = parseSingleDate(parts[0])
    const end = parseSingleDate(parts[1])

    if (start && end) {
      const startDate = new Date(start + 'T00:00:00')
      const endDate = new Date(end + 'T00:00:00')

      if (startDate <= endDate) {
        selectedStart.value = startDate
        selectedEnd.value = endDate
        previewStart.value = null
        previewEnd.value = null
        emit('update:modelValue', [start, end])
        inputText.value = `${formatDisplayDate(start)} - ${formatDisplayDate(end)}`
        // Navigate calendar to show the start date's month
        const [year, month, day] = start.split('-')
        currentMonth.value = new Date(parseInt(year), parseInt(month) - 1, 1)
        isOpen.value = false
      }
    } else if (parts.length >= 1) {
      // Partial range - update calendar based on what's typed so far
      const partial = parsePartialDate(parts[0])
      if (partial.year || partial.month || partial.day) {
        const year = partial.year || currentMonth.value.getFullYear()
        const month = partial.month || (currentMonth.value.getMonth() + 1)
        currentMonth.value = new Date(year, month - 1, 1)

        // Update preview for first date
        if (partial.day && partial.month && partial.year) {
          const fullDate = `${partial.year}-${String(partial.month).padStart(2, '0')}-${String(partial.day).padStart(2, '0')}`
          previewStart.value = new Date(fullDate + 'T00:00:00')
        } else {
          previewStart.value = null
        }
      }

      // Check if there's a partial second date
      if (parts.length >= 2) {
        const partial2 = parsePartialDate(parts[1])
        if (partial2.day && partial2.month && partial2.year) {
          const fullDate = `${partial2.year}-${String(partial2.month).padStart(2, '0')}-${String(partial2.day).padStart(2, '0')}`
          previewEnd.value = new Date(fullDate + 'T00:00:00')
        }
      }
    }
  } else if (parts.length === 1 && parts[0].length > 0) {
    // User typing first date only
    const partial = parsePartialDate(parts[0])
    if (partial.year || partial.month || partial.day) {
      const year = partial.year || currentMonth.value.getFullYear()
      const month = partial.month || (currentMonth.value.getMonth() + 1)
      currentMonth.value = new Date(year, month - 1, 1)

      // Update preview
      if (partial.day && partial.month && partial.year) {
        const fullDate = `${partial.year}-${String(partial.month).padStart(2, '0')}-${String(partial.day).padStart(2, '0')}`
        previewStart.value = new Date(fullDate + 'T00:00:00')
      } else {
        previewStart.value = null
      }
      previewEnd.value = null
    }
  }
}

const applyPreset = (start: Date, end: Date) => {
  selectedStart.value = start
  selectedEnd.value = end
  const startStr = formatDateString(start)
  const endStr = formatDateString(end)
  emit('update:modelValue', [startStr, endStr])
  inputText.value = `${formatDisplayDate(startStr)} - ${formatDisplayDate(endStr)}`
  isOpen.value = false
}

const setPreset = (days: number) => {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - days)
  applyPreset(start, end)
}

const setThisMonth = () => {
  const today = new Date()
  const start = new Date(today.getFullYear(), today.getMonth(), 1)
  applyPreset(start, new Date())
}

const setLastMonth = () => {
  const today = new Date()
  const start = new Date(today.getFullYear(), today.getMonth() - 1, 1)
  const end = new Date(today.getFullYear(), today.getMonth(), 0)
  applyPreset(start, end)
}

const setThisYear = () => {
  const today = new Date()
  const start = new Date(today.getFullYear(), 0, 1)
  applyPreset(start, new Date())
}

const setLastYear = () => {
  const today = new Date()
  const start = new Date(today.getFullYear() - 1, 0, 1)
  const end = new Date(today.getFullYear() - 1, 11, 31)
  applyPreset(start, end)
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
  <div class="relative" ref="wrapperRef">
    <!-- Input field -->
    <input
      ref="inputRef"
      v-model="inputText"
      type="text"
      placeholder="DD/MM/YYYY - DD/MM/YYYY"
      @click="() => { updatePopupPosition(); isOpen = true }"
      @input="handleInput"
      @focus="() => { updatePopupPosition(); isOpen = true }"
      class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] focus:outline-none focus:border-primary-500/40 transition-colors"
    />

    <!-- Popup teleported to body -->
    <Teleport to="body">
    <div v-if="isOpen" ref="popupRef" :style="{ position: 'fixed', top: popupStyle.top, left: popupStyle.left, width: '900px', maxWidth: 'calc(100vw - 32px)' }" class="z-[9999] bg-surface-card rounded-[10px] shadow-2xl p-0 border border-border">
      <div class="flex h-[400px]">
        <!-- Left sidebar with presets -->
        <div class="w-[150px] border-r border-border p-4 flex flex-col gap-2">
          <button type="button" @click="setPreset(0)" class="text-left px-3 py-2 hover:bg-white/[0.05] rounded text-sm text-text-secondary hover:text-text-primary transition-colors">Today</button>
          <button type="button" @click="setPreset(1)" class="text-left px-3 py-2 hover:bg-white/[0.05] rounded text-sm text-text-secondary hover:text-text-primary transition-colors">Yesterday</button>
          <button type="button" @click="setPreset(6)" class="text-left px-3 py-2 hover:bg-white/[0.05] rounded text-sm text-text-secondary hover:text-text-primary transition-colors">Last 7 Days</button>
          <button type="button" @click="setPreset(29)" class="text-left px-3 py-2 hover:bg-white/[0.05] rounded text-sm text-text-secondary hover:text-text-primary transition-colors">Last 30 Days</button>
          <button type="button" @click="setThisMonth" class="text-left px-3 py-2 hover:bg-white/[0.05] rounded text-sm text-text-secondary hover:text-text-primary transition-colors">This Month</button>
          <button type="button" @click="setLastMonth" class="text-left px-3 py-2 hover:bg-white/[0.05] rounded text-sm text-text-secondary hover:text-text-primary transition-colors">1 Month Ago</button>
          <button type="button" @click="setThisYear" class="text-left px-3 py-2 hover:bg-white/[0.05] rounded text-sm text-text-secondary hover:text-text-primary transition-colors">This Year</button>
          <button type="button" @click="setLastYear" class="text-left px-3 py-2 hover:bg-white/[0.05] rounded text-sm text-text-secondary hover:text-text-primary transition-colors">1 Year Ago</button>
          <button type="button" class="text-left px-3 py-2 text-primary-500 font-semibold rounded text-sm hover:bg-white/[0.05] transition-colors">Custom</button>
        </div>

        <!-- Right side with calendars -->
        <div class="flex-1 p-4">
          <!-- Navigation -->
          <div class="flex items-center justify-between mb-4">
            <button type="button" @click="prevMonth" class="text-text-muted hover:text-text-primary text-lg transition-colors">‹</button>
            <div class="flex gap-8">
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
              <div class="flex items-center gap-2">
                <div class="w-[140px]">
                  <AppSelect
                    v-model="nextMonthValue"
                    :options="monthOptions"
                  />
                </div>
                <div class="w-[100px]">
                  <AppSelect
                    v-model="nextYearValue"
                    :options="yearOptions"
                  />
                </div>
              </div>
            </div>
            <button type="button" @click="nextMonth" class="text-text-muted hover:text-text-primary text-lg transition-colors">›</button>
          </div>

          <!-- Calendars -->
          <div class="flex gap-8">
            <div v-for="(month, idx) in months" :key="idx" class="flex-1">
              <!-- Days of week -->
              <div class="grid grid-cols-7 gap-1 mb-2">
                <div v-for="day in dayNames" :key="day" class="text-center text-xs font-semibold text-text-muted">{{ day }}</div>
              </div>
              <!-- Days -->
              <div class="grid grid-cols-7 gap-1">
                <button
                  type="button"
                  v-for="(day, i) in getDaysForMonth(month)"
                  :key="i"
                  @click="day && selectDate(day as number, month)"
                  :disabled="!day"
                  :class="{
                    'bg-primary-500 text-white rounded font-semibold': day && (isSelectedDate(day as number, month) === 'start' || isSelectedDate(day as number, month) === 'end'),
                    'bg-primary-500/20 text-text-primary': day && isBetweenDates(day as number, month),
                    'text-text-muted cursor-not-allowed': !day,
                    'text-text-primary hover:bg-white/[0.05]': day && !isBetweenDates(day as number, month) && !isSelectedDate(day as number, month),
                  }"
                  class="h-8 flex items-center justify-center text-sm rounded transition-colors"
                >
                  {{ day }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Bottom bar -->
      <div class="flex items-center justify-between border-t border-border p-4 bg-white/[0.02] rounded-b-[10px]">
        <span v-if="selectedStart && selectedEnd" class="text-sm text-text-secondary">
          {{ formatDisplayDate(formatDateString(selectedStart)) }} - {{ formatDisplayDate(formatDateString(selectedEnd)) }}
        </span>
        <span v-else class="text-sm text-text-muted">Select date range</span>
        <div class="flex gap-2">
          <button type="button" @click="clearRange" class="px-4 py-2 text-sm text-text-secondary hover:text-text-primary transition-colors">Clear</button>
          <button type="button" @click="applyRange" class="px-4 py-2 text-sm gradient-brand text-white rounded-[9px] transition-opacity hover:opacity-90 font-semibold">Apply</button>
        </div>
      </div>
    </div>
    </Teleport>
  </div>
</template>
