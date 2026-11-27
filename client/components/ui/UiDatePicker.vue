<template>
  <UiField :label="label" :required="required" :error="error" :hint="hint" :data-testid="testid">
    <div class="grid grid-cols-3 gap-2" role="group" :aria-label="label">
      <UiSelect
        :model-value="parts.day"
        :options="dayOptions"
        :placeholder="$t('ui.datePicker.day')"
        :disabled="disabled"
        :size="UiSize.Sm"
        :testid="testid ? `${testid}-day` : undefined"
        @update:model-value="change('day', String($event))"
      />
      <UiSelect
        :model-value="parts.month"
        :options="monthOptions"
        :placeholder="$t('ui.datePicker.month')"
        :disabled="disabled"
        :size="UiSize.Sm"
        :testid="testid ? `${testid}-month` : undefined"
        @update:model-value="change('month', String($event))"
      />
      <UiSelect
        :model-value="parts.year"
        :options="yearOptions"
        :placeholder="$t('ui.datePicker.year')"
        :disabled="disabled"
        :size="UiSize.Sm"
        :testid="testid ? `${testid}-year` : undefined"
        @update:model-value="change('year', String($event))"
      />
    </div>
  </UiField>
</template>

<script setup lang="ts">
/**
 * UiDatePicker: a calendar date chosen from three kit lists (day, month, year), never from the browser's own date popup,
 * which cannot be themed and differs per browser.
 *
 * The model is an ISO `yyyy-MM-dd` text, or an empty text while the three parts do not name a real day. Choosing a month
 * a chosen day does not exist in (the 31st of a short month) drops the day, so the model never holds an impossible date.
 */
import { computed, reactive, watch } from 'vue'
import {
  type DateParts,
  MONTH_VALUES,
  daysInMonth,
  joinIsoDate,
  splitIsoDate,
  twoDigitValues,
  yearValues
} from '~/utils/datePicker'
import { UiSize } from '~/utils/ui/enums'

/** Props of UiDatePicker. */
interface Props {
  /** Selected day as `yyyy-MM-dd` (v-model); empty for none. */
  modelValue?: string
  /** Label text above the three lists. */
  label?: string
  /** Marks the field required and adds the asterisk. */
  required?: boolean
  /** Disables the three lists. */
  disabled?: boolean
  /** Error line; also switches the lists to their error look. */
  error?: string
  /** Neutral help line, hidden while an error shows. */
  hint?: string
  /** `data-testid` of the group; the lists add `-day`, `-month` and `-year`. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  modelValue: '',
  label: undefined,
  required: false,
  disabled: false,
  error: undefined,
  hint: undefined,
  testid: undefined
})

/** Events the component raises. */
const emit = defineEmits<{
  /** The chosen day changed; an empty text while the parts do not name a real day. */
  'update:modelValue': [value: string]
}>()

/** Translates a key and gives the active language, which names the months. */
const { locale } = useI18n()

/** The three parts as chosen; a part may be set before the others, so the lists keep what the reader picked. */
const parts = reactive<DateParts>(splitIsoDate(props.modelValue))

// A model set from outside (a stored value loaded later) refills the lists; the picker's own emission is the same text, so no loop.
watch(() => props.modelValue, (value) => {
  if (value !== joinIsoDate(parts)) {
    Object.assign(parts, splitIsoDate(value))
  }
})

/** The day choices of the chosen month and year. */
const dayOptions = computed(() =>
  twoDigitValues(daysInMonth(parts.year, parts.month)).map(value => ({ value, label: String(Number(value)) })))

/** The month choices, named in the active language. */
const monthOptions = computed(() => {
  const names = new Intl.DateTimeFormat(locale.value, { month: 'long' })
  return MONTH_VALUES.map(value => ({ value, label: names.format(new Date(2000, Number(value) - 1, 1)) }))
})

/** The year choices, the latest first. */
const yearOptions = computed(() => yearValues(new Date().getFullYear()).map(value => ({ value, label: value })))

/**
 * Applies one choice and reports the date the three parts make.
 *
 * @param part - The part that changed.
 * @param value - Its new two or four digit text.
 */
const change = (part: keyof DateParts, value: string): void => {
  parts[part] = value
  // A day the new month does not have is dropped rather than kept as an impossible date.
  if (parts.day && Number(parts.day) > daysInMonth(parts.year, parts.month)) {
    parts.day = ''
  }
  emit('update:modelValue', joinIsoDate(parts))
}
</script>
