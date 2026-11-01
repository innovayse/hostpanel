<script setup lang="ts">
/**
 * Single-date picker: a typed `DD/MM/YYYY` field with a month grid and quick choices.
 *
 * The model is an ISO `YYYY-MM-DD` string (or null). Typing a complete date commits it; a partial
 * date moves the calendar so the reader sees where it will land. `direction` picks the quick
 * choices: `past` (yesterday, 7 days ago and so on) or `future` (tomorrow, in 7 days and so on), which
 * replaces the former separate future picker. Colours are theme tokens, so the popup follows the
 * light/dark switch. `data-testid` and classes land on the root element.
 */
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiPanelTransition from '@/components/ui/UiPanelTransition.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
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

/** Approximate popup height, used to decide whether it fits below the field. */
const POPUP_HEIGHT = 450

/** Whether the popup is open. */
const isOpen = ref(false)

/** Whether the popup opens above the field because there is no room below. */
const openAbove = ref(false)

/** First day of the month the grid shows. */
const currentMonth = ref(new Date())

/** Root element, used for outside-click detection. */
const popupRef = ref<HTMLElement | null>(null)

/** The text field, measured to place the popup. */
const inputRef = ref<HTMLInputElement | null>(null)

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

/** Width class of the quick-choice column; future captions are longer. */
const quickPanelWidthClass = computed<string>(() => (props.direction === UiDatePickerDirection.Future ? 'w-48' : 'w-36'))

/** Quick choice matching the model right now. */
const selectedQuick = computed<QuickDate | null>(() => matchQuickDate(props.modelValue, props.direction))

/** Cells of the visible month. */
const monthDays = computed<(number | null)[]>(() => getDaysForMonth(currentMonth.value))

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

/** Decides whether the popup opens above or below the field. */
const checkPopupPosition = (): void => {
  if (!inputRef.value) {
    return
  }
  const rect = inputRef.value.getBoundingClientRect()
  const spaceBelow = window.innerHeight - rect.bottom
  openAbove.value = spaceBelow < POPUP_HEIGHT && rect.top > POPUP_HEIGHT
}

/** Opens the popup unless the picker is disabled. */
const openPopup = (): void => {
  if (!props.disabled) {
    isOpen.value = true
  }
}

/** Closes the popup. */
const closePopup = (): void => {
  isOpen.value = false
}

/**
 * Closes the popup on a click outside the picker.
 *
 * @param e - The document click.
 */
const handleClickOutside = (e: MouseEvent): void => {
  if (popupRef.value && !popupRef.value.contains(e.target as Node)) {
    isOpen.value = false
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
  'px-3 py-2 text-left text-[0.82rem] rounded transition-colors',
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
  isOpen.value = false
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
    isOpen.value = true
    return
  }
  commitDate(quickDateFor(choice, props.direction))
}

/** Clears the date and closes the popup. */
const clearDate = (): void => {
  emit('update:modelValue', null)
  isOpen.value = false
}

/** Shows the previous month. */
const prevMonth = (): void => {
  currentMonth.value = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth() - 1)
}

/** Shows the next month. */
const nextMonth = (): void => {
  currentMonth.value = new Date(currentMonth.value.getFullYear(), currentMonth.value.getMonth() + 1)
}

watch(isOpen, async (opened) => {
  if (opened) {
    // Wait for the render so the popup can be measured against the viewport.
    await nextTick()
    checkPopupPosition()
  }
})

// An outside change of the model (form reset, loaded record) shows in the field.
watch(() => props.modelValue, syncInputText)

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
  initFromModel()
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
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
      :class="dateFieldClasses"
      class="disabled:opacity-50 disabled:cursor-not-allowed"
      @click="openPopup"
      @focus="openPopup"
      @input="handleInput"
    >

    <UiPanelTransition>
      <div
        v-if="isOpen"
        :class="[datePopupClasses, openAbove ? 'bottom-full mb-2' : 'top-full mt-2']"
        class="absolute left-0 z-50 overflow-hidden"
      >
        <div class="flex">
          <div :class="quickPanelWidthClass" class="bg-surface-hover border-r border-border p-3 flex flex-col gap-1">
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

          <div class="p-4 w-80">
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
                :key="i"
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
              <button type="button" data-testid="date-picker-close" :class="dateActionClasses" @click="closePopup">{{ t('shared.close') }}</button>
            </div>
          </div>
        </div>
      </div>
    </UiPanelTransition>
  </div>
</template>
