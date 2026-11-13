<script setup lang="ts">
/**
 * Phone field with a country-code picker.
 *
 * Driven by the `phoneNumber` / `countryCode` pair. The code picker is
 * the kit's searchable `UiSelect`, joined to the number field, over the geo atlas phone-code options, so the listbox, its keyboard
 * contract and its outside-click handling are the kit's one implementation. The search matches the
 * name, native name, ISO code and calling code. The flag of each country is the emoji the atlas
 * carries for it, so the component loads no image from a host.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiSelect from '@/components/ui/UiSelect.vue'
import { useGeoOptions } from '@/composables/useGeoOptions'

/** Props for UiPhoneInput. */
const props = defineProps<{
  /** The national number (two-way bound with `v-model:phone-number`). */
  phoneNumber?: string | null
  /** ISO code of the selected country (two-way bound with `v-model:country-code`). */
  countryCode?: string
}>()

const emit = defineEmits<{
  /** Emitted with the national number as the user types. */
  'update:phoneNumber': [value: string | null]
  /** Emitted with the ISO code of the country the user picks. */
  'update:countryCode': [value: string]
}>()

/** Translator for the strings this component builds itself. */
const { t } = useI18n()

/** ISO code of the country offered when none is given. */
const DEFAULT_COUNTRY = 'US'

/** Sample national number for the countries that have one; every other country shows a generic hint. */
const SAMPLE_NUMBERS: Readonly<Record<string, string>> = {
  US: '201-555-0123',
  AM: '77 123456',
  GB: '7700 900000',
  CA: '416-555-0123',
  FR: '1 42 34 56 78',
  DE: '30 123456',
  IT: '06 1234 5678',
  ES: '91 123 45 67',
  RU: '495 123-45-67',
  JP: '90-1234-5678',
  CN: '10 1234 5678',
  IN: '98765 43210',
}

/** Phone-code options of the geo atlas, the list every phone picker shares. */
const { phoneCodeOptions } = useGeoOptions()

/** ISO code of the selected country (the default when none is given). */
const selectedCountryCode = computed<string>({
  /**
   * Reads the selected country.
   *
   * @returns The ISO code bound by the parent, or the default.
   */
  get: () => props.countryCode || DEFAULT_COUNTRY,
  /**
   * Publishes the picked country.
   *
   * @param value - The ISO code picked in the select.
   */
  set: (value) => {
    emit('update:countryCode', value)
  },
})

/** The national number, bound to the text field. */
const phoneNumberModel = computed<string>({
  /**
   * Reads the national number.
   *
   * @returns The number shown in the text field.
   */
  get: () => props.phoneNumber || '',
  /**
   * Publishes the typed national number.
   *
   * @param value - The text typed in the field.
   */
  set: (value) => {
    emit('update:phoneNumber', value)
  },
})

/** Sample number of the selected country, shown as the field hint. */
const currentPlaceholder = computed<string>(() => SAMPLE_NUMBERS[selectedCountryCode.value] ?? t('shared.phoneNumber2'))
</script>

<template>
  <div class="flex items-center gap-0 w-full">
    <!-- Country code picker: the kit's searchable listbox -->
    <UiSelect
      v-model="selectedCountryCode"
      :options="phoneCodeOptions"
      :placeholder="t('shared.phone')"
      :aria-label="t('ui.phoneInput.countryCode')"
      searchable
      wide
      attached
      class="shrink-0"
      data-testid="phone-country-select"
    />

    <!-- Phone number input -->
    <input
      v-model="phoneNumberModel"
      type="text"
      :placeholder="currentPlaceholder"
      data-testid="phone-number-input"
      class="flex-1 min-w-0 h-10.5 px-3.5 text-sm text-text-primary bg-surface-field border border-border border-l-0 rounded-r-control placeholder:text-text-muted outline-none focus-visible:border-text-accent focus-visible:ring-2 focus-visible:ring-text-accent/40 transition-colors"
    >
  </div>
</template>
