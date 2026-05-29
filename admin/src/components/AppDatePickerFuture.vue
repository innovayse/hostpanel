<script setup lang="ts">
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
  const popupHeight = 450
  const spaceBelow = window.innerHeight - inputRect.bottom
  const spaceAbove = inputRect.top

  openAbove.value = spaceBelow < popupHeight && spaceAbove > popupHeight
}

const formatDisplayDate = (dateStr: string) => {
  const [year, month, day] = dateStr.split('-')
  return `${day}/${month}/${year}`
}

const displayText = computed(() => {
  if (!props.modelValue) return props.placeholder ?? 'Select date...'
  return formatDisplayDate(props.modelValue)
})

const parseDateInput = (input: string) => {
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

  if (!input || input.trim() === '') {
    emit('update:modelValue', null)
    currentMonth.value = new Date()
    previewDay.value = null
    previewMonth.value = null
    previewYear.value = null
    return
  }

  const parsed = parseDateInput(input)
  if (parsed) {
    emit('update:modelValue', parsed)
    const [year, month, day] = parsed.split('-')
    const yearNum = parseInt(year)
    const monthNum = parseInt(month)
    const dayNum = parseInt(day)
    currentMonth.value = new Date(yearNum, monthNum - 1, 1)
    previewDay.value = null
    previewMonth.value = null
    previewYear.value = null
  } else {
    const partial = parsePartialDate(input)

    if (partial.year || partial.month || partial.day) {
      const year = partial.year || currentMonth.value.getFullYear()
      const month = partial.month || (currentMonth.value.getMonth() + 1)

      currentMonth.value = new Date(year, month - 1, 1)

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
  if (previewDay.value !== null && previewMonth.value !== null && previewYear.value !== null) {
    return (
      day === previewDay.value &&
      month.getMonth() === previewMonth.value - 1 &&
      month.getFullYear() === previewYear.value
    )
  }

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

  const tomorrow = new Date(today)
  tomorrow.setDate(tomorrow.getDate() + 1)
  if (selectedDate === formatDateString(tomorrow)) {
    return 'tomorrow'
  }

  const sevenDaysFromNow = new Date(today)
  sevenDaysFromNow.setDate(sevenDaysFromNow.getDate() + 7)
  if (selectedDate === formatDateString(sevenDaysFromNow)) {
    return '7days'
  }

  const oneMonthFromNow = new Date(today)
  oneMonthFromNow.setMonth(oneMonthFromNow.getMonth() + 1)
  if (selectedDate === formatDateString(oneMonthFromNow)) {
    return '1month'
  }

  const oneYearFromNow = new Date(today)
  oneYearFromNow.setFullYear(oneYearFromNow.getFullYear() + 1)
  if (selectedDate === formatDateString(oneYearFromNow)) {
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

const selectQuickDate = (offset: string) => {
  const today = new Date()
  let date: Date

  if (offset === 'today') {
    date = new Date(today)
  } else if (offset === 'tomorrow') {
    date = new Date(today)
    date.setDate(date.getDate() + 1)
  } else if (offset === '7days') {
    date = new Date(today)
    date.setDate(date.getDate() + 7)
  } else if (offset === '1month') {
    date = new Date(today)
    date.setMonth(date.getMonth() + 1)
  } else if (offset === '1year') {
    date = new Date(today)
    date.setFullYear(date.getFullYear() + 1)
  } else {
    date = today
  }

  const year = date.getFullYear()
  const monthStr = String(date.getMonth() + 1).padStart(2, '0')
  const dayStr = String(date.getDate()).padStart(2, '0')
  const dateString = `${year}-${monthStr}-${dayStr}`
  emit('update:modelValue', dateString)
  inputText.value = formatDisplayDate(dateString)
  previewDay.value = null
  previewMonth.value = null
  previewYear.value = null
  currentMonth.value = new Date(date.getFullYear(), date.getMonth(), 1)
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
        <div class="w-48 bg-white/[0.02] border-r border-border p-3 flex flex-col gap-1">
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
            @click="selectQuickDate('tomorrow')"
            :class="getSelectedQuickDate() === 'tomorrow' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            Tomorrow
          </button>
          <button
            type="button"
            @click="selectQuickDate('7days')"
            :class="getSelectedQuickDate() === '7days' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            In 7 Days
          </button>
          <button
            type="button"
            @click="selectQuickDate('1month')"
            :class="getSelectedQuickDate() === '1month' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            1 Month From Now
          </button>
          <button
            type="button"
            @click="selectQuickDate('1year')"
            :class="getSelectedQuickDate() === '1year' ? 'bg-primary-500 text-white' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.05]'"
            class="px-3 py-2 text-left text-[0.82rem] rounded transition-colors"
          >
            1 Year From Now
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
  </div>
</template>
