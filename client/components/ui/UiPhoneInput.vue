<template>
  <div class="w-full">
    <label v-if="label" :for="inputId" class="block text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-600 dark:text-primary-400">*</span>
    </label>

    <!-- Row: the country picker (which anchors its panel to this row) and the national number -->
    <div class="relative flex items-stretch gap-2">
      <UiPhoneCountryPicker
        :model-value="country"
        :disabled="disabled"
        :error="Boolean(error)"
        testid="phone-country-trigger"
        @update:model-value="onCountry"
      />

      <div class="relative min-w-0 flex-1">
        <input
          :id="inputId"
          :value="national"
          type="tel"
          inputmode="tel"
          autocomplete="tel-national"
          :placeholder="t('ui.phoneExample')"
          :required="required"
          :disabled="disabled"
          :aria-invalid="error ? 'true' : undefined"
          :data-testid="testid"
          :class="inputClasses"
          @input="onNational(($event.target as HTMLInputElement).value)"
        >
        <AlertCircle
          v-if="error"
          :size="20"
          :stroke-width="2"
          class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-red-500"
        />
      </div>
    </div>

    <p v-if="error" class="mt-2 text-sm text-red-600 dark:text-red-400">{{ error }}</p>
    <p v-else-if="hint" class="mt-2 text-sm text-gray-500 dark:text-gray-400">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
/**
 * PhoneInput (`UiPhoneInput`). An international phone field: a country picker (flag and calling
 * code, searchable list) beside the field for the national number.
 *
 * The stored value keeps its format: the number in international format (`+37410123456`) when it
 * is valid for the chosen country, otherwise the text as typed behind the calling code. A value
 * that arrives from outside is split back into country and national number; one this field just
 * emitted is not, so typing never moves the chosen country under the reader.
 */
import { computed, ref, useId, watch } from 'vue'
import { AlertCircle } from 'lucide-vue-next'
import { DEFAULT_PHONE_COUNTRY, composePhoneValue, splitPhoneValue } from '~/utils/phoneCountries'
import { UiSize } from '~/utils/ui/enums'
import { CONTROL_SIZE_CLASSES, fieldStateClasses } from '~/utils/ui/tones'

/** Props of PhoneInput. */
interface Props {
  /** DOM id of the number input; generated when omitted so the label is always bound. */
  id?: string
  /** Label text above the field. */
  label?: string
  /** The number (v-model). */
  modelValue?: string
  /** Marks the field required and adds the asterisk. */
  required?: boolean
  /** Disables the field. */
  disabled?: boolean
  /** Error line; also switches the field to its error look. */
  error?: string
  /** Neutral help line, hidden while an error shows. */
  hint?: string
  /** `data-testid` of the number input. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  label: undefined,
  modelValue: undefined,
  required: false,
  disabled: false,
  error: undefined,
  hint: undefined,
  testid: undefined,
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** The number changed. */
  'update:modelValue': [value: string]
}>()

/** Translator for the strings this component builds itself. */
const { t } = useI18n()

/** Generated id used when the caller passes none. */
const generatedId = useId()

/** The id bound to both the label and the number input. */
const inputId = computed(() => props.id ?? generatedId)

/** ISO code of the chosen country; Armenia, the operator's home market, until the reader picks. */
const country = ref(DEFAULT_PHONE_COUNTRY)

/** The national part of the number, as typed. */
const national = ref('')

/** The last value this field emitted, so the echo from the parent is not split again. */
let lastEmitted: string | undefined

/** Class list of the number input: the kit's field frame at the standard size. */
const inputClasses = computed(() => [
  fieldStateClasses({ error: Boolean(props.error), disabled: props.disabled }),
  'placeholder:text-gray-500 dark:placeholder:text-gray-400',
  CONTROL_SIZE_CLASSES[UiSize.Md],
  props.error ? 'pr-10' : '',
].filter(Boolean).join(' '))

/**
 * Publishes the value for the current country and number.
 */
const publish = (): void => {
  lastEmitted = composePhoneValue(country.value, national.value)
  emit('update:modelValue', lastEmitted)
}

/**
 * Takes the number the reader typed.
 *
 * @param value The text of the number input.
 */
const onNational = (value: string): void => {
  national.value = value
  publish()
}

/**
 * Takes the country the reader picked.
 *
 * @param iso ISO code of the picked country.
 */
const onCountry = (iso: string): void => {
  country.value = iso
  publish()
}

/** Splits a value that came from outside into the picker and the number input. */
watch(() => props.modelValue, (value) => {
  if (value === lastEmitted) {
    return
  }
  const parts = splitPhoneValue(value ?? '', country.value)
  country.value = parts.iso
  national.value = parts.national
}, { immediate: true })
</script>
