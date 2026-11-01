<script setup lang="ts">
/**
 * Date-range picker: a typed `DD/MM/YYYY - DD/MM/YYYY` field with two month grids and presets.
 *
 * The model is a `[start, end]` pair of ISO `YYYY-MM-DD` strings (or null). Clicking two days
 * picks a range and Apply commits it; a preset commits at once; typing a complete range commits
 * it, a partial one moves the calendar. The popup is teleported to the body so a clipped parent
 * cannot cut it off, and is placed through CSS variables set from script (no inline style).
 * Colours are theme tokens, so the popup follows the light/dark switch.
 */
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiPanelTransition from '@/components/ui/UiPanelTransition.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { splitIsoDate } from '@/lib/format'
import { translate } from '@/lib/translate'
import {
  RangeEdge,
  buildYearOptions,
  dateActionClasses,
  dateArrowClasses,
  dateCellBaseClasses,
  dateCellEmptyClasses,
  dateCellIdleClasses,
  dateCellInRangeClasses,
  dateCellSelectedClasses,
  dateFieldClasses,
  datePopupClasses,
  datePresetIdleClasses,
  dateRangePresets,
  formatDisplayDate,
  formatIsoDate,
  getDaysForMonth,
  parsePartialDate,
  parseTypedDate,
  partialToDate,
  presetRange,
} from '@/utils/ui/datePicker'
import type { DateRangePreset } from '@/utils/ui/datePicker'

const { t } = useI18n()

/** Props for UiDateRangePicker. */
const props = defineProps<{
  /** Selected range as ISO `[start, end]`, or null for none. */
  modelValue: [string, string] | null
  /** Hint shown in the empty field; the range format when omitted or empty. */
  placeholder?: string
}>()

const emit = defineEmits<{
  /** Emitted with the new ISO range, or null when it is cleared. */
  'update:modelValue': [value: [string, string] | null]
}>()

/** Widest the popup grows, in pixels. */
const POPUP_MAX_WIDTH = 900

/** Gap kept between the popup and the viewport edge, in pixels. */
const POPUP_EDGE_GAP = 16

/** Whether the popup is open. */
const isOpen = ref(false)

/** First day of the range being picked. */
const selectedStart = ref<Date | null>(null)

/** Last day of the range being picked. */
const selectedEnd = ref<Date | null>(null)

/** First day of the left-hand month. */
const currentMonth = ref(new Date())

/** Popup panel, outside-click detection and CSS-variable target. */
const popupRef = ref<HTMLElement | null>(null)

/** Field wrapper, measured to place the popup. */
const wrapperRef = ref<HTMLElement | null>(null)

/** Text in the field. */
const inputText = ref('')

/** Start typed so far, highlighted before the range is complete. */
const previewStart = ref<Date | null>(null)

/** End typed so far, highlighted before the range is complete. */
const previewEnd = ref<Date | null>(null)

/** Popup top edge in viewport pixels. */
const popupTop = ref(0)

/** Popup left edge in viewport pixels. */
const popupLeft = ref(0)

/** Month names for the month selects. */
const monthNames = Array.from({ length: 12 }, (_, i) => translate(`calendar.months.${i + 1}`))

/** Weekday names for the grid headers. */
const dayNames = Array.from({ length: 7 }, (_, i) => translate(`calendar.weekdays.${i}`))

/** Options of the month selects. */
const monthOptions = computed(() => monthNames.map((name, i) => ({ value: String(i), label: name })))

/** Options of the year selects. */
const yearOptions = buildYearOptions()

/** The field hint: the caller's, else the range format. */
const fieldPlaceholder = computed<string>(() => props.placeholder || t('ui.uiDateRangePicker.ddMmYyyyDdMm'))

/** The two months on show. */
const months = computed<Date[]>(() => {
  const first = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth())
  return [first, new Date(first.getFullYear(), first.getMonth() + 1)]
})

/** Cells of each shown month. */
const monthCells = computed<(number | null)[][]>(() => months.value.map(getDaysForMonth))

/** Left month select value. */
const currentMonthValue = computed<string>({
  /**
   * Reads the left month index.
   *
   * @returns The value for the select.
   */
  get: () => String(currentMonth.value.getMonth()),
  /**
   * Moves the grid to the picked month.
   *
   * @param value - The picked value, as the select emits it.
   */
  set: (value) => {
    currentMonth.value = new Date(currentMonth.value.getFullYear(), parseInt(value), 1)
  },
})

/** Left year select value. */
const currentYearValue = computed<string>({
  /**
   * Reads the left year.
   *
   * @returns The value for the select.
   */
  get: () => String(currentMonth.value.getFullYear()),
  /**
   * Moves the grid to the picked year.
   *
   * @param value - The picked value, as the select emits it.
   */
  set: (value) => {
    currentMonth.value = new Date(parseInt(value), currentMonth.value.getMonth(), 1)
  },
})

/** Right month select value; choosing a month makes the left grid the month before it. */
const nextMonthValue = computed<string>({
  /**
   * Reads the right month index.
   *
   * @returns The value for the select.
   */
  get: () => String(months.value[1]?.getMonth() ?? 0),
  /**
   * Makes the left grid the month before the picked one.
   *
   * @param value - The picked value, as the select emits it.
   */
  set: (value) => {
    const picked = parseInt(value)
    const year = months.value[1]?.getFullYear() ?? new Date().getFullYear()
    currentMonth.value = new Date(year, picked - 1, 1)
  },
})

/** Right year select value; choosing a year keeps the right month and moves the left one. */
const nextYearValue = computed<string>({
  /**
   * Reads the right year.
   *
   * @returns The value for the select.
   */
  get: () => String(months.value[1]?.getFullYear() ?? new Date().getFullYear()),
  /**
   * Moves the left grid relative to the picked year.
   *
   * @param value - The picked value, as the select emits it.
   */
  set: (value) => {
    const month = months.value[1]?.getMonth() ?? 0
    currentMonth.value = new Date(parseInt(value), month - 1, 1)
  },
})

/** The range being picked, rendered for the bottom bar; null until both ends are set. */
const pickedLabel = computed<string | null>(() => {
  if (!selectedStart.value || !selectedEnd.value) {
    return null
  }
  return `${formatDisplayDate(formatIsoDate(selectedStart.value))} - ${formatDisplayDate(formatIsoDate(selectedEnd.value))}`
})

/**
 * Renders a range for the field.
 *
 * @param start - ISO start day.
 * @param end - ISO end day.
 * @returns `DD/MM/YYYY - DD/MM/YYYY`.
 */
const formatRangeText = (start: string, end: string): string =>
  `${formatDisplayDate(start)} - ${formatDisplayDate(end)}`

/** Writes the model into the field text. */
const syncInputText = (): void => {
  inputText.value = props.modelValue ? formatRangeText(props.modelValue[0], props.modelValue[1]) : ''
}

/** Forgets the half-typed range highlight. */
const clearPreview = (): void => {
  previewStart.value = null
  previewEnd.value = null
}

/** Computes where the popup sits: under the field, kept inside the viewport. */
const updatePopupPosition = (): void => {
  if (!wrapperRef.value) {
    return
  }
  const rect = wrapperRef.value.getBoundingClientRect()
  const width = Math.min(POPUP_MAX_WIDTH, window.innerWidth - 2 * POPUP_EDGE_GAP)
  let left = rect.left
  if (left + width > window.innerWidth - POPUP_EDGE_GAP) {
    left = window.innerWidth - width - POPUP_EDGE_GAP
  }
  popupTop.value = rect.bottom + 8
  popupLeft.value = Math.max(left, 8)
}

/** Hands the computed position to the popup as CSS variables its classes read. */
const applyPopupPosition = async (): Promise<void> => {
  // The popup is v-if'd, so it exists only after the next render.
  await nextTick()
  popupRef.value?.style.setProperty('--dp-top', `${popupTop.value}px`)
  popupRef.value?.style.setProperty('--dp-left', `${popupLeft.value}px`)
}

/** Opens the popup under the field. */
const openPopup = (): void => {
  updatePopupPosition()
  isOpen.value = true
  void applyPopupPosition()
}

/**
 * Closes the popup on a click outside both the popup and the field.
 *
 * @param e - The document click.
 */
const handleClickOutside = (e: MouseEvent): void => {
  const target = e.target as Node
  const outsidePopup = !popupRef.value || !popupRef.value.contains(target)
  const outsideField = !wrapperRef.value || !wrapperRef.value.contains(target)
  if (outsidePopup && outsideField) {
    isOpen.value = false
  }
}

/**
 * Tells which edge of the range a day is, preferring what is being typed.
 *
 * @param day - Day of the month shown.
 * @param month - First day of that month.
 * @returns The edge, or null for an ordinary day.
 */
const edgeOf = (day: number, month: Date): RangeEdge | null => {
  const time = new Date(month.getFullYear(), month.getMonth(), day).getTime()
  if (previewStart.value && time === previewStart.value.getTime()) {
    return RangeEdge.Start
  }
  if (previewEnd.value && time === previewEnd.value.getTime()) {
    return RangeEdge.End
  }
  if (selectedStart.value && time === selectedStart.value.getTime()) {
    return RangeEdge.Start
  }
  if (selectedEnd.value && time === selectedEnd.value.getTime()) {
    return RangeEdge.End
  }
  return null
}

/**
 * Tells whether a day lies strictly inside the range, preferring what is being typed.
 *
 * @param day - Day of the month shown.
 * @param month - First day of that month.
 * @returns True for a day between the two edges.
 */
const isBetween = (day: number, month: Date): boolean => {
  const current = new Date(month.getFullYear(), month.getMonth(), day)
  if (previewStart.value && previewEnd.value) {
    return current > previewStart.value && current < previewEnd.value
  }
  if (!selectedStart.value || !selectedEnd.value) {
    return false
  }
  return current > selectedStart.value && current < selectedEnd.value
}

/**
 * Class list of one grid cell.
 *
 * @param day - Day of the month, or null for padding.
 * @param month - First day of the month shown.
 * @returns Literal Tailwind classes for the cell's state.
 */
const dayClasses = (day: number | null, month: Date): string[] => {
  if (day === null) {
    return [dateCellBaseClasses, dateCellEmptyClasses]
  }
  if (edgeOf(day, month)) {
    return [dateCellBaseClasses, dateCellSelectedClasses]
  }
  if (isBetween(day, month)) {
    return [dateCellBaseClasses, dateCellInRangeClasses]
  }
  return [dateCellBaseClasses, dateCellIdleClasses]
}

/**
 * Picks a day: first click starts, second ends (swapping if earlier), a third restarts.
 *
 * @param day - Day of the month; null padding is ignored.
 * @param month - First day of the month shown.
 */
const selectDay = (day: number | null, month: Date): void => {
  if (day === null) {
    return
  }
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

/** Commits the picked range and closes the popup. */
const applyRange = (): void => {
  if (!selectedStart.value || !selectedEnd.value) {
    return
  }
  const start = formatIsoDate(selectedStart.value)
  const end = formatIsoDate(selectedEnd.value)
  emit('update:modelValue', [start, end])
  inputText.value = formatRangeText(start, end)
  isOpen.value = false
}

/** Clears the range. */
const clearRange = (): void => {
  selectedStart.value = null
  selectedEnd.value = null
  inputText.value = ''
  emit('update:modelValue', null)
}

/**
 * Commits a range at once.
 *
 * @param start - First day.
 * @param end - Last day.
 */
const applyPreset = (start: Date, end: Date): void => {
  selectedStart.value = start
  selectedEnd.value = end
  const startIso = formatIsoDate(start)
  const endIso = formatIsoDate(end)
  emit('update:modelValue', [startIso, endIso])
  inputText.value = formatRangeText(startIso, endIso)
  isOpen.value = false
}

/**
 * Commits the range a preset stands for.
 *
 * @param preset - The preset button pressed.
 */
const selectPreset = (preset: DateRangePreset): void => {
  const [start, end] = presetRange(preset)
  applyPreset(start, end)
}

/**
 * Moves the grid to the month of what has been typed, and returns its full date if complete.
 *
 * @param text - One typed date, possibly partial.
 * @returns The date when day, month and year are all valid, else null.
 */
const followPartial = (text: string): Date | null => {
  const partial = parsePartialDate(text)
  if (partial.year || partial.month || partial.day) {
    const year = partial.year || currentMonth.value.getFullYear()
    const month = partial.month || currentMonth.value.getMonth() + 1
    currentMonth.value = new Date(year, month - 1, 1)
  }
  return partialToDate(partial)
}

/** Reads the typed text: commits a complete range, or previews a partial one in the grid. */
const handleInput = (): void => {
  const input = inputText.value
  if (input.trim() === '') {
    emit('update:modelValue', null)
    selectedStart.value = null
    selectedEnd.value = null
    clearPreview()
    currentMonth.value = new Date()
    return
  }

  // Split on whitespace and dashes: "25052026-31052026" and "25/05/2026 - 31/05/2026" both work.
  // Under noUncheckedIndexedAccess `parts[n]` is `string | undefined`, so destructure once.
  const [firstPart, secondPart] = input.split(/[\s-]+/).filter(part => part.length > 0)
  if (!firstPart) {
    return
  }

  if (secondPart) {
    const start = parseTypedDate(firstPart)
    const end = parseTypedDate(secondPart)
    if (start && end) {
      const startDate = new Date(`${start}T00:00:00`)
      const endDate = new Date(`${end}T00:00:00`)
      if (startDate <= endDate) {
        selectedStart.value = startDate
        selectedEnd.value = endDate
        clearPreview()
        emit('update:modelValue', [start, end])
        inputText.value = formatRangeText(start, end)
        const { year, month } = splitIsoDate(start)
        currentMonth.value = new Date(year, month - 1, 1)
        isOpen.value = false
      }
      return
    }
    previewStart.value = followPartial(firstPart)
    const typedEnd = partialToDate(parsePartialDate(secondPart))
    if (typedEnd) {
      previewEnd.value = typedEnd
    }
    return
  }

  previewStart.value = followPartial(firstPart)
  previewEnd.value = null
}

/** Shows the previous month. */
const prevMonth = (): void => {
  currentMonth.value = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth() - 1)
}

/** Shows the next month. */
const nextMonth = (): void => {
  currentMonth.value = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth() + 1)
}

onMounted(() => {
  syncInputText()
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<template>
  <div ref="wrapperRef" class="relative">
    <input
      v-model="inputText"
      type="text"
      data-testid="date-range-input"
      :placeholder="fieldPlaceholder"
      :class="dateFieldClasses"
      @click="openPopup"
      @focus="openPopup"
      @input="handleInput"
    >

    <Teleport to="body">
      <UiPanelTransition>
        <div
          v-if="isOpen"
          ref="popupRef"
          :class="datePopupClasses"
          class="fixed z-[9999] p-0 w-[900px] max-w-[calc(100vw-32px)] top-[var(--dp-top)] left-[var(--dp-left)]"
        >
          <div class="flex h-[400px]">
            <div class="w-[150px] border-r border-border p-4 flex flex-col gap-2">
              <button
                v-for="item in dateRangePresets"
                :key="item.preset"
                type="button"
                :data-testid="`date-range-preset-${item.preset}`"
                :class="datePresetIdleClasses"
                class="text-left px-3 py-2 rounded text-sm transition-colors"
                @click="selectPreset(item.preset)"
              >
                {{ t(item.labelKey) }}
              </button>
              <div class="text-left px-3 py-2 text-primary-600 light:text-primary-700 font-semibold rounded text-sm bg-primary-500/10">{{ t('shared.custom') }}</div>
            </div>

            <div class="flex-1 p-4">
              <div class="flex items-center justify-between mb-4">
                <button type="button" data-testid="date-range-prev" :class="dateArrowClasses" @click="prevMonth">‹</button>
                <div class="flex gap-8">
                  <div class="flex items-center gap-2">
                    <div class="w-[140px]">
                      <UiSelect v-model="currentMonthValue" :options="monthOptions" :placeholder="t('ui.uiDatePicker.month')" />
                    </div>
                    <div class="w-[100px]">
                      <UiSelect v-model="currentYearValue" :options="yearOptions" :placeholder="t('ui.uiDatePicker.year')" />
                    </div>
                  </div>
                  <div class="flex items-center gap-2">
                    <div class="w-[140px]">
                      <UiSelect v-model="nextMonthValue" :options="monthOptions" :placeholder="t('ui.uiDatePicker.month')" />
                    </div>
                    <div class="w-[100px]">
                      <UiSelect v-model="nextYearValue" :options="yearOptions" :placeholder="t('ui.uiDatePicker.year')" />
                    </div>
                  </div>
                </div>
                <button type="button" data-testid="date-range-next" :class="dateArrowClasses" @click="nextMonth">›</button>
              </div>

              <div class="flex gap-8">
                <div v-for="(month, idx) in months" :key="idx" class="flex-1">
                  <div class="grid grid-cols-7 gap-1 mb-2">
                    <div v-for="name in dayNames" :key="name" class="text-center text-xs font-semibold text-text-secondary">{{ name }}</div>
                  </div>
                  <div class="grid grid-cols-7 gap-1">
                    <button
                      v-for="(day, i) in monthCells[idx]"
                      :key="i"
                      type="button"
                      :data-testid="`date-range-day-${idx}-${i}`"
                      :disabled="day === null"
                      :class="dayClasses(day, month)"
                      @click="selectDay(day, month)"
                    >
                      {{ day }}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="flex items-center justify-between border-t border-border p-4 bg-surface-hover rounded-b-[10px]">
            <span v-if="pickedLabel" class="text-sm text-text-secondary">{{ pickedLabel }}</span>
            <span v-else class="text-sm text-text-secondary">{{ t('ui.uiDateRangePicker.selectDateRange') }}</span>
            <div class="flex gap-2">
              <button type="button" data-testid="date-range-clear" :class="dateActionClasses" class="px-4 py-2" @click="clearRange">{{ t('shared.clear') }}</button>
              <button
                type="button"
                data-testid="date-range-apply"
                class="px-4 py-2 text-sm gradient-brand text-white rounded-[9px] outline-none transition-opacity hover:opacity-90 focus-visible:ring-2 focus-visible:ring-primary-500/50 font-semibold"
                @click="applyRange"
              >
                {{ t('shared.apply') }}
              </button>
            </div>
          </div>
        </div>
      </UiPanelTransition>
    </Teleport>
  </div>
</template>
