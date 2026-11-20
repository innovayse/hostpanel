<script setup lang="ts">
/**
 * Single-date picker: a typed `DD/MM/YYYY` field with a month grid and quick choices.
 *
 * The model is an ISO `YYYY-MM-DD` string (or null). Typing a complete date commits it; a partial
 * date moves the calendar so the reader sees where it will land. `direction` picks the quick
 * choices: `past` (yesterday, 7 days ago and so on) or `future` (tomorrow, in 7 days and so on), which
 * replaces the former separate future picker. Colours are theme tokens, so the popup follows the
 * light/dark switch. The popup is drawn in the page body (a teleport) and placed under the field with fixed
 * positioning, so no card around the field can paint over it; on a phone (below `sm`) it is a bottom sheet
 * ({@link UiFloatingSurface}) and the field opens it by a tap and raises no keyboard. `data-testid` and classes land on the root element.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiFloatingSurface from '@/components/ui/UiFloatingSurface.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { useDrawerPresentation } from '@/composables/useDrawerPresentation'
import { useDropdown } from '@/composables/useDropdown'
import { UiDrawerPresentation } from '@/utils/ui/drawer'
import { splitIsoDate } from '@/lib/format'
import { translate } from '@/lib/translate'
import {
  QuickDate,
  UiDatePickerDirection,
  buildYearOptions,
  dateActionClasses,
  dateArrowClasses,
  dateCellBaseClasses,
  dateCellEmptyClasses,
  dateCellIdleClasses,
  dateCellSelectedClasses,
  dateFieldClasses,
  datePopupClasses,
  datePresetActiveClasses,
  datePresetIdleClasses,
  formatDisplayDate,
  formatIsoDate,
  getDayKeysForMonth,
  getDaysForMonth,
  matchQuickDate,
  parsePartialDate,
  parseTypedDate,
  quickDateFor,
  quickDateLabelKeys,
  quickDateOrder,
} from '@/utils/ui/datePicker'

const { t } = useI18n()

/** Props for UiDatePicker. */
const props = withDefaults(defineProps<{
  /** Selected day as ISO `YYYY-MM-DD`, or null for none. */
  modelValue: string | null
  /** Hint shown in the empty field; the date format when omitted or empty. */
  placeholder?: string
  /** Direction of the quick choices. */
  direction?: UiDatePickerDirection
  /** Blocks typing and the popup. */
  disabled?: boolean
}>(), {
  placeholder: undefined,
  direction: UiDatePickerDirection.Past,
  disabled: false,
})

const emit = defineEmits<{
  /** Emitted with the new ISO day, or null when the date is cleared. */
  'update:modelValue': [value: string | null]
}>()

/** Approximate popup height: the popup opens above the field when less than this is free below it. */
const POPUP_HEIGHT = 450

/** Gap between the field and the popup, in pixels. */
const POPUP_GAP_PX = 8

/** Open flag and outside-pointer handling shared with the other floating lists; `panel` is the teleported popup. */
const { open: isOpen, root: popupRef, trigger: inputRef, panel, show, hide } = useDropdown()

/** The shape the screen asks for: on a phone the popup is a bottom sheet. */
const presentation = useDrawerPresentation()

/**
 * Whether the popup is a bottom sheet. The field then opens it by a tap only and raises no keyboard (`inputmode="none"`), so
 * closing the sheet, which gives the focus back to the field, never opens it again.
 */
const isSheet = computed<boolean>(() => presentation.value === UiDrawerPresentation.Sheet)

/** Classes of the popover: clipped to the kit's popup look (the place and the surface are the floating surface's). */
const popupClasses = `overflow-hidden ${datePopupClasses}`

/** First day of the month the grid shows. */
const currentMonth = ref(new Date())

/** Text in the field. */
const inputText = ref('')

/** Day typed so far, highlighted before the date is complete. */
const previewDay = ref<number | null>(null)

/** Month typed so far (1-12). */
const previewMonth = ref<number | null>(null)

/** Year typed so far. */
const previewYear = ref<number | null>(null)

/** Month names for the month select. */
const monthNames = Array.from({ length: 12 }, (_, i) => translate(`calendar.months.${i + 1}`))

/** Weekday names for the grid header. */
const dayNames = Array.from({ length: 7 }, (_, i) => translate(`calendar.weekdays.${i}`))

/** Options of the month select. */
const monthOptions = computed(() => monthNames.map((name, i) => ({ value: String(i), label: name })))

/** Options of the year select. */
const yearOptions = buildYearOptions()

/** The field hint: the caller's, else the date format. */
const fieldPlaceholder = computed<string>(() => props.placeholder || t('ui.uiDatePicker.ddMmYyyy'))

/** Width class of the quick-choice column (a row of chips on a phone); future captions are longer. */
const quickPanelWidthClass = computed<string>(() => (props.direction === UiDatePickerDirection.Future ? 'sm:w-48' : 'sm:w-36'))

/** Quick choice matching the model right now. */
const selectedQuick = computed<QuickDate | null>(() => matchQuickDate(props.modelValue, props.direction))

/** Cells of the visible month. */
const monthDays = computed<(number | null)[]>(() => getDaysForMonth(currentMonth.value))

/** ISO day of each visible cell, the cell's list key. */
const monthDayKeys = computed<string[]>(() => getDayKeysForMonth(currentMonth.value))

/** Month select value, kept in step with the grid. */
const currentMonthValue = computed<string>({
  /**
   * Reads the month index.
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

/** Year select value, kept in step with the grid. */
const currentYearValue = computed<string>({
  /**
   * Reads the year.
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

/** Writes the model into the field text. */
const syncInputText = (): void => {
  inputText.value = props.modelValue ? formatDisplayDate(props.modelValue) : ''
}

/** Forgets the half-typed date highlight. */
const clearPreview = (): void => {
  previewDay.value = null
  previewMonth.value = null
  previewYear.value = null
}

/** Opens the popup unless the picker is disabled. */
const openPopup = (): void => {
  if (!props.disabled) {
    show()
  }
}

/** Opens the popup when the field takes focus (tabbing in), except for a sheet, which a tap opens. */
const openOnFocus = (): void => {
  if (!isSheet.value) {
    openPopup()
  }
}

/** Moves the grid to the model's month once, and shows the model in the field. */
const initFromModel = (): void => {
  syncInputText()
  if (props.modelValue) {
    const { year, month } = splitIsoDate(props.modelValue)
    currentMonth.value = new Date(year, month - 1, 1)
  }
}

/** Reads the typed text: commits a complete date, or previews a partial one in the grid. */
const handleInput = (): void => {
  const input = inputText.value
  if (input.trim() === '') {
    emit('update:modelValue', null)
    currentMonth.value = new Date()
    clearPreview()
    return
  }

  const parsed = parseTypedDate(input)
  if (parsed) {
    emit('update:modelValue', parsed)
    const { year, month } = splitIsoDate(parsed)
    currentMonth.value = new Date(year, month - 1, 1)
    clearPreview()
    return
  }

  const partial = parsePartialDate(input)
  if (partial.year || partial.month || partial.day) {
    const year = partial.year || currentMonth.value.getFullYear()
    const month = partial.month || currentMonth.value.getMonth() + 1
    currentMonth.value = new Date(year, month - 1, 1)
    previewDay.value = partial.day || null
    previewMonth.value = partial.month || null
    previewYear.value = partial.year || null
  }
}

/**
 * Tells whether a grid day is the selected one (or the half-typed one while typing).
 *
 * @param day - Day of the visible month.
 * @returns True when the day is highlighted as selected.
 */
const isSelectedDay = (day: number): boolean => {
  if (previewDay.value !== null && previewMonth.value !== null && previewYear.value !== null) {
    return (
      day === previewDay.value
      && currentMonth.value.getMonth() === previewMonth.value - 1
      && currentMonth.value.getFullYear() === previewYear.value
    )
  }
  if (!props.modelValue) {
    return false
  }
  return formatIsoDate(new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth(), day)) === props.modelValue
}

/**
 * Class list of one grid cell.
 *
 * @param day - Day of the month, or null for padding.
 * @returns Literal Tailwind classes for the cell's state.
 */
const dayClasses = (day: number | null): string[] => {
  if (day === null) {
    return [dateCellBaseClasses, dateCellEmptyClasses]
  }
  return [dateCellBaseClasses, isSelectedDay(day) ? dateCellSelectedClasses : dateCellIdleClasses]
}

/**
 * Class list of one quick-choice button.
 *
 * @param choice - The quick choice the button stands for.
 * @returns Active or idle classes.
 */
const quickClasses = (choice: QuickDate): string[] => [
  'px-3 py-2 text-left text-[0.82rem] rounded transition-colors max-sm:shrink-0 max-sm:whitespace-nowrap',
  selectedQuick.value === choice ? datePresetActiveClasses : datePresetIdleClasses,
]

/**
 * Commits a day: emits it, shows it, moves the grid and closes the popup.
 *
 * @param date - The chosen day.
 */
const commitDate = (date: Date): void => {
  const iso = formatIsoDate(date)
  emit('update:modelValue', iso)
  inputText.value = formatDisplayDate(iso)
  clearPreview()
  currentMonth.value = new Date(date.getFullYear(), date.getMonth(), 1)
  hide()
}

/**
 * Picks a day of the visible month.
 *
 * @param day - Day of the visible month; null padding cells are ignored.
 */
const selectDay = (day: number | null): void => {
  if (day === null) {
    return
  }
  commitDate(new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth(), day))
}

/**
 * Picks a quick choice. `Custom` just keeps the popup open for the grid.
 *
 * @param choice - The quick choice.
 */
const selectQuick = (choice: QuickDate): void => {
  if (choice === QuickDate.Custom) {
    show()
    return
  }
  commitDate(quickDateFor(choice, props.direction))
}

/** Clears the date and closes the popup. */
const clearDate = (): void => {
  emit('update:modelValue', null)
  hide()
}

/** Shows the previous month. */
const prevMonth = (): void => {
  currentMonth.value = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth() - 1)
}

/** Shows the next month. */
const nextMonth = (): void => {
  currentMonth.value = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth() + 1)
}

// An outside change of the model (form reset, loaded record) shows in the field.
watch(() => props.modelValue, syncInputText)

onMounted(initFromModel)
</script>

<template>
  <div ref="popupRef" class="relative">
    <input
      ref="inputRef"
      v-model="inputText"
      type="text"
      data-testid="date-picker-input"
      :placeholder="fieldPlaceholder"
      :disabled="disabled"
      :inputmode="isSheet ? 'none' : undefined"
      :class="dateFieldClasses"
      @click="openPopup"
      @focus="openOnFocus"
      @input="handleInput"
    >

    <!-- The popup: in the page body (no card around the field can paint over it) or a bottom sheet on a phone -->
    <UiFloatingSurface
      :open="isOpen"
      :anchor="inputRef"
      :title="fieldPlaceholder"
      :min-room="POPUP_HEIGHT"
      :gap="POPUP_GAP_PX"
      :popover-class="popupClasses"
      @close="hide(true)"
      @panel="panel = $event"
    >
      <div class="flex flex-col sm:flex-row">
        <div :class="quickPanelWidthClass" class="bg-surface-hover border-b sm:border-b-0 sm:border-r border-border p-3 flex flex-row sm:flex-col gap-1 max-sm:overflow-x-auto">
          <button
            v-for="choice in quickDateOrder"
            :key="choice"
            type="button"
            :data-testid="`date-picker-quick-${choice}`"
            :class="quickClasses(choice)"
            @click="selectQuick(choice)"
          >
            {{ t(quickDateLabelKeys[direction][choice]) }}
          </button>
        </div>

        <div class="p-4 w-full sm:w-80">
          <div class="flex items-center justify-between mb-4">
            <button type="button" data-testid="date-picker-prev" :class="dateArrowClasses" @click="prevMonth">‹</button>
            <div class="flex items-center gap-2">
              <div class="w-[140px]">
                <UiSelect v-model="currentMonthValue" :options="monthOptions" :placeholder="t('ui.uiDatePicker.month')" />
              </div>
              <div class="w-[100px]">
                <UiSelect v-model="currentYearValue" :options="yearOptions" :placeholder="t('ui.uiDatePicker.year')" />
              </div>
            </div>
            <button type="button" data-testid="date-picker-next" :class="dateArrowClasses" @click="nextMonth">›</button>
          </div>

          <div class="grid grid-cols-7 gap-1 mb-2">
            <div v-for="name in dayNames" :key="name" class="text-center text-xs font-semibold text-text-secondary">{{ name }}</div>
          </div>
          <div class="grid grid-cols-7 gap-1">
            <button
              v-for="(day, i) in monthDays"
              :key="monthDayKeys[i]"
              type="button"
              :data-testid="`date-picker-day-${i}`"
              :disabled="day === null"
              :class="dayClasses(day)"
              @click="selectDay(day)"
            >
              {{ day }}
            </button>
          </div>

          <div class="flex items-center justify-between border-t border-border mt-4 pt-4">
            <button type="button" data-testid="date-picker-clear" :class="dateActionClasses" @click="clearDate">{{ t('shared.clear') }}</button>
            <button type="button" data-testid="date-picker-close" :class="dateActionClasses" @click="hide()">{{ t('shared.close') }}</button>
          </div>
        </div>
      </div>
    </UiFloatingSurface>
  </div>
</template>
