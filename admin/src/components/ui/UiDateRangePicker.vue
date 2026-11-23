<script setup lang="ts">
/**
 * Date-range picker: a typed `DD/MM/YYYY - DD/MM/YYYY` field with two month grids (one below `md`) and presets (a chip row on a phone).
 *
 * The model is a `[start, end]` pair of ISO `YYYY-MM-DD` strings (or null). Clicking two days
 * picks a range and Apply commits it; a preset commits at once; typing a complete range commits
 * it, a partial one moves the calendar. The popup is teleported to the body so a clipped parent
 * cannot cut it off, and is placed under the field (above it when there is no room below) through CSS variables
 * set from script (no inline style); on a phone (below `sm`) it is a bottom sheet ({@link UiFloatingSurface}) and the field
 * opens it by a tap and raises no keyboard.
 * Colours are theme tokens, so the popup follows the light/dark switch.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import UiFloatingSurface from '@/components/ui/UiFloatingSurface.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { useDrawerPresentation } from '@/composables/useDrawerPresentation'
import { useDropdown } from '@/composables/useDropdown'
import { UiDrawerPresentation } from '@/utils/ui/drawer'
import { splitIsoDate } from '@/lib/format'
import { translate } from '@/lib/translate'
import {
  RangeEdge,
  buildYearOptions,
  dateArrowClasses,
  dateCellBaseClasses,
  dateCellEmptyClasses,
  dateCellIdleClasses,
  dateCellInRangeClasses,
  dateCellRangeEndClasses,
  dateCellRangeStartClasses,
  dateCellSelectedClasses,
  dateFieldClasses,
  datePopupClasses,
  datePresetActiveClasses,
  datePresetIdleClasses,
  dateRangePresets,
  formatDisplayDate,
  formatIsoDate,
  getDayKeysForMonth,
  getDaysForMonth,
  matchDateRangePreset,
  parsePartialDate,
  parseTypedDate,
  partialToDate,
  presetRange,
} from '@/utils/ui/datePicker'
import type { DateRangePreset } from '@/utils/ui/datePicker'
import { UiActionKind } from '@/utils/ui/actions'

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

/** Widest the popup grows, in pixels (its class is `w-[900px]`). */
const POPUP_MAX_WIDTH = 900

/** Height of the popup in pixels: it opens above the field when less than this is free below it. */
const POPUP_HEIGHT = 400

/** Gap between the field and the popup, in pixels. */
const POPUP_GAP_PX = 8

/** Open flag and outside-pointer handling shared with the other floating lists; `panel` is the teleported popup. */
const { open: isOpen, root: wrapperRef, trigger: inputRef, panel, show, hide } = useDropdown()

/** The shape the screen asks for: on a phone the popup is a bottom sheet. */
const presentation = useDrawerPresentation()

/**
 * Whether the popup is a bottom sheet. The field then opens it by a tap only and raises no keyboard (`inputmode="none"`), so
 * closing the sheet, which gives the focus back to the field, never opens it again.
 */
const isSheet = computed<boolean>(() => presentation.value === UiDrawerPresentation.Sheet)

/** Classes of the popover in the kit's popup look (the lists of its month and year selects open later in the body, so they stay above it), as wide as the screen allows; the place and the surface are the floating surface's. */
const popupClasses = `p-0 w-[900px] max-w-[calc(100vw-32px)] ${datePopupClasses}`

/** First day of the range being picked. */
const selectedStart = ref<Date | null>(null)

/** Last day of the range being picked. */
const selectedEnd = ref<Date | null>(null)

/** First day of the left-hand month. */
const currentMonth = ref(new Date())

/** Text in the field. */
const inputText = ref('')

/** Start typed so far, highlighted before the range is complete. */
const previewStart = ref<Date | null>(null)

/** End typed so far, highlighted before the range is complete. */
const previewEnd = ref<Date | null>(null)

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

/** ISO day of each cell of each shown month, the cell's list key. */
const monthCellKeys = computed<string[][]>(() => months.value.map(getDayKeysForMonth))

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

/** The preset the picked range stands for, or null when it is a custom range or none is picked. */
const activePreset = computed<DateRangePreset | null>(() =>
  selectedStart.value && selectedEnd.value
    ? matchDateRangePreset([formatIsoDate(selectedStart.value), formatIsoDate(selectedEnd.value)])
    : null,
)

/** Whether a complete range is picked that no preset stands for, which marks "Custom". */
const isCustomRange = computed<boolean>(() => Boolean(selectedStart.value && selectedEnd.value) && activePreset.value === null)

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

/** Loads the model into the picked range and opens the calendar at the month the range starts in. */
const syncSelection = (): void => {
  clearPreview()
  if (!props.modelValue) {
    selectedStart.value = null
    selectedEnd.value = null
    return
  }
  const [start, end] = props.modelValue.map(splitIsoDate)
  if (!start || !end) {
    return
  }
  selectedStart.value = new Date(start.year, start.month - 1, start.day)
  selectedEnd.value = new Date(end.year, end.month - 1, end.day)
  currentMonth.value = new Date(start.year, start.month - 1, 1)
}

/** Forgets the half-typed range highlight. */
const clearPreview = (): void => {
  previewStart.value = null
  previewEnd.value = null
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
 * Class list of a range edge: a cap square on the side that joins the band, or a full pill for a one-day range.
 *
 * @param edge - Which edge the day is.
 * @returns Literal Tailwind classes for the cap.
 */
const edgeClasses = (edge: RangeEdge): string => {
  const isOneDay = Boolean(selectedStart.value && selectedEnd.value && selectedStart.value.getTime() === selectedEnd.value.getTime())
  if (isOneDay || !selectedEnd.value) {
    return dateCellSelectedClasses
  }
  return edge === RangeEdge.Start ? dateCellRangeStartClasses : dateCellRangeEndClasses
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
  const edge = edgeOf(day, month)
  if (edge) {
    return [dateCellBaseClasses, edgeClasses(edge)]
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
  hide()
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
  hide()
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
        hide()
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

/** Opens the popup when the field takes focus (tabbing in), except for a sheet, which a tap opens. */
const openOnFocus = (): void => {
  if (!isSheet.value) {
    show()
  }
}

onMounted(syncInputText)

// A popup that opens shows the model, not a half-picked range left from before.
watch(isOpen, (opened) => {
  if (opened) {
    syncSelection()
  }
})
</script>

<template>
  <div ref="wrapperRef" class="relative">
    <input
      ref="inputRef"
      v-model="inputText"
      type="text"
      data-testid="date-range-input"
      :placeholder="fieldPlaceholder"
      :inputmode="isSheet ? 'none' : undefined"
      :class="dateFieldClasses"
      @click="show"
      @focus="openOnFocus"
      @input="handleInput"
    >

    <!-- The popup: in the page body (a clipped parent cannot cut it off) or a bottom sheet on a phone -->
    <UiFloatingSurface
      :open="isOpen"
      :anchor="inputRef"
      :title="fieldPlaceholder"
      :min-room="POPUP_HEIGHT"
      :gap="POPUP_GAP_PX"
      :keep-inside-width="POPUP_MAX_WIDTH"
      :popover-class="popupClasses"
      @close="hide(true)"
      @panel="panel = $event"
    >
      <div class="flex flex-col md:h-[400px] md:flex-row">
        <div class="shrink-0 min-h-0 md:w-max flex flex-row md:flex-col gap-1 overflow-x-auto md:overflow-x-visible md:overflow-y-auto border-b md:border-b-0 md:border-r border-border p-3 md:p-4">
          <button
            v-for="item in dateRangePresets"
            :key="item.preset"
            type="button"
            :data-testid="`date-range-preset-${item.preset}`"
            :class="item.preset === activePreset ? datePresetActiveClasses : datePresetIdleClasses"
            :aria-pressed="item.preset === activePreset"
            class="shrink-0 whitespace-nowrap text-left px-3 py-2 max-sm:py-3 rounded-lg text-sm transition-colors"
            @click="selectPreset(item.preset)"
          >
            {{ t(item.labelKey) }}
          </button>
          <div
            data-testid="date-range-preset-custom"
            :class="isCustomRange ? datePresetActiveClasses : 'text-text-secondary'"
            class="shrink-0 whitespace-nowrap text-left px-3 py-2 rounded-lg text-sm"
          >
            {{ t('shared.custom') }}
          </div>
        </div>

        <div class="flex-1 min-w-0 p-3 md:p-4">
          <div class="flex items-center justify-between mb-4">
            <button type="button" data-testid="date-range-prev" :class="dateArrowClasses" @click="prevMonth">‹</button>
            <div class="flex min-w-0 gap-4 lg:gap-8">
              <div class="flex min-w-0 items-center gap-2">
                <div class="w-[140px] min-w-0">
                  <UiSelect v-model="currentMonthValue" :options="monthOptions" :placeholder="t('ui.uiDatePicker.month')" />
                </div>
                <div class="w-[100px] min-w-0">
                  <UiSelect v-model="currentYearValue" :options="yearOptions" :placeholder="t('ui.uiDatePicker.year')" />
                </div>
              </div>
              <div class="flex min-w-0 items-center gap-2 max-md:hidden" data-testid="date-range-next-selects">
                <div class="w-[140px] min-w-0">
                  <UiSelect v-model="nextMonthValue" :options="monthOptions" :placeholder="t('ui.uiDatePicker.month')" />
                </div>
                <div class="w-[100px] min-w-0">
                  <UiSelect v-model="nextYearValue" :options="yearOptions" :placeholder="t('ui.uiDatePicker.year')" />
                </div>
              </div>
            </div>
            <button type="button" data-testid="date-range-next" :class="dateArrowClasses" @click="nextMonth">›</button>
          </div>

          <div class="flex gap-4 lg:gap-8">
            <div v-for="(month, idx) in months" :key="formatIsoDate(month)" class="min-w-0 flex-1" :class="idx > 0 ? 'max-md:hidden' : ''" :data-testid="`date-range-month-${idx}`">
              <div class="grid grid-cols-7 gap-1 mb-2">
                <div v-for="name in dayNames" :key="name" class="text-center text-xs font-semibold text-text-secondary">{{ name }}</div>
              </div>
              <div class="grid grid-cols-7 gap-y-1">
                <button
                  v-for="(day, i) in monthCells[idx]"
                  :key="monthCellKeys[idx]?.[i]"
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

      <div class="flex flex-wrap items-center justify-between gap-3 border-t border-border p-4 rounded-b-card">
        <span v-if="pickedLabel" class="text-sm text-text-secondary">{{ pickedLabel }}</span>
        <span v-else class="text-sm text-text-secondary">{{ t('ui.uiDateRangePicker.selectDateRange') }}</span>
        <div class="flex gap-2 max-sm:w-full max-sm:justify-end">
          <UiButton :action="UiActionKind.Clear" :variant="UiButtonVariant.Ghost" type="button" data-testid="date-range-clear" @click="clearRange">{{ t('shared.clear') }}</UiButton>
          <UiButton :action="UiActionKind.Apply" :variant="UiButtonVariant.Primary" type="button" data-testid="date-range-apply" @click="applyRange">
            {{ t('shared.apply') }}
          </UiButton>
        </div>
      </div>
    </UiFloatingSurface>
  </div>
</template>
