<script setup lang="ts">
/**
 * Country picker: a searchable `UiSelect` over a fixed list of countries.
 *
 * The model is the ISO 3166 alpha-2 code. Names come from the geo atlas, the list every country picker shares.
 */
import { computed } from 'vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { useGeoOptions } from '@/composables/useGeoOptions'

import { useI18n } from 'vue-i18n'

const { t } = useI18n()

/** Props for UiCountrySelect. */
const props = defineProps<{
  /** ISO code of the selected country. */
  modelValue: string
  /** Text shown while nothing is selected. */
  placeholder?: string
}>()

const emit = defineEmits<{
  /** Emitted with the ISO code of the country the user picks. */
  'update:modelValue': [value: string]
}>()

/** Country list of the geo atlas, the same source every other country picker reads. */
const { geoCountries } = useGeoOptions()

/** The countries as `UiSelect` options: value is the ISO code, label the country name; entries without either are left out. */
const countryOptions = computed(() =>
  geoCountries.flatMap(c => (c.iso2 && c.name ? [{ value: String(c.iso2), label: c.name }] : [])),
)

/** Two-way bridge between the prop and the inner select. */
const selectedValue = computed({
  /**
   * Reads the bound code.
   *
   * @returns The ISO code the parent holds.
   */
  get: () => props.modelValue,
  /**
   * Publishes the picked code.
   *
   * @param value - The ISO code picked in the select.
   */
  set: (value) => {
    emit('update:modelValue', value)
  },
})
</script>

<template>
  <UiSelect
    v-model="selectedValue"
    :options="countryOptions"
    :placeholder="placeholder || t('ui.uiCountrySelect.selectCountry')"
    searchable
    wide
  />
</template>
