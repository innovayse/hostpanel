<script setup lang="ts">
import { computed, ref } from 'vue'

const props = defineProps<{
  modelValue: string | null
  phoneNumber?: string | null
  countryCode?: string
  placeholder?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
  'update:phoneNumber': [value: string | null]
  'update:countryCode': [value: string]
}>()

const isOpen = ref(false)

const phoneCountries = [
  { value: 'AM', label: 'Armenia (Հայաստան)', code: '+374', flag: '🇦🇲', placeholder: '77 123456' },
  { value: 'US', label: 'United States', code: '+1', flag: '🇺🇸', placeholder: '201-555-0123' },
  { value: 'GB', label: 'United Kingdom', code: '+44', flag: '🇬🇧', placeholder: '7700 900000' },
  { value: 'CA', label: 'Canada', code: '+1', flag: '🇨🇦', placeholder: '416-555-0123' },
  { value: 'FR', label: 'France', code: '+33', flag: '🇫🇷', placeholder: '1 42 34 56 78' },
  { value: 'DE', label: 'Germany', code: '+49', flag: '🇩🇪', placeholder: '30 123456' },
  { value: 'IT', label: 'Italy', code: '+39', flag: '🇮🇹', placeholder: '06 1234 5678' },
  { value: 'ES', label: 'Spain', code: '+34', flag: '🇪🇸', placeholder: '91 123 45 67' },
  { value: 'RU', label: 'Russia', code: '+7', flag: '🇷🇺', placeholder: '495 123-45-67' },
  { value: 'JP', label: 'Japan', code: '+81', flag: '🇯🇵', placeholder: '90-1234-5678' },
  { value: 'CN', label: 'China', code: '+86', flag: '🇨🇳', placeholder: '10 1234 5678' },
  { value: 'IN', label: 'India', code: '+91', flag: '🇮🇳', placeholder: '98765 43210' },
]

const selectedCountryCode = computed({
  get: () => props.countryCode || 'AM',
  set: (value) => emit('update:countryCode', value)
})

const phoneNumber = computed({
  get: () => props.phoneNumber || '',
  set: (value) => emit('update:phoneNumber', value)
})

const selectedCountry = computed(() =>
  phoneCountries.find(c => c.value === selectedCountryCode.value)
)

const currentPlaceholder = computed(() =>
  selectedCountry.value?.placeholder || '77 123456'
)

const selectCountry = (code: string) => {
  emit('update:countryCode', code)
  isOpen.value = false
}
</script>

<template>
  <div class="relative flex items-center gap-0 w-full">
    <!-- Country Code Button (Dropdown trigger) -->
    <button
      type="button"
      @click="isOpen = !isOpen"
      class="flex items-center gap-2 px-3 py-2 bg-white/[0.05] border border-border border-r-0 rounded-l-[9px] cursor-pointer hover:bg-white/[0.08] transition-colors whitespace-nowrap"
    >
      <svg
        :key="selectedCountry?.value"
        :class="`fi fi-${selectedCountry?.value.toLowerCase()}`"
        style="width: 24px; height: 18px;"
      />
      <span class="text-[0.82rem] font-medium text-text-primary">{{ selectedCountry?.code }}</span>
      <span class="text-[0.7rem] text-text-muted">▼</span>
    </button>

    <!-- Phone Number Input -->
    <input
      v-model="phoneNumber"
      type="text"
      :placeholder="currentPlaceholder"
      class="flex-1 px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border border-l-0 rounded-r-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
    />

    <!-- Country Dropdown -->
    <div
      v-if="isOpen"
      class="absolute top-full left-0 mt-1 z-50 bg-surface-card border border-border rounded-lg shadow-lg max-h-72 overflow-y-auto w-80"
    >
      <div
        v-for="country in phoneCountries"
        :key="country.value"
        @click="selectCountry(country.value)"
        :class="selectedCountryCode === country.value ? 'bg-primary-500 text-white' : 'text-text-primary hover:bg-white/[0.05]'"
        class="px-3 py-2.5 text-[0.82rem] cursor-pointer transition-colors flex items-center gap-2.5"
      >
        <svg
          :class="`fi fi-${country.value.toLowerCase()}`"
          style="width: 24px; height: 18px;"
        />
        <span>{{ country.label }}</span>
        <span class="ml-auto text-[0.75rem] font-medium opacity-75">{{ country.code }}</span>
      </div>
    </div>

    <!-- Close dropdown on outside click -->
    <div v-if="isOpen" @click="isOpen = false" class="fixed inset-0 z-40" />
  </div>
</template>

<style scoped>
/* Flag icons CSS - compact inline style */
.fi {
  background-size: contain;
  background-position: 50%;
  background-repeat: no-repeat;
  display: inline-block;
}

.fi-am { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/am.svg'); }
.fi-us { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/us.svg'); }
.fi-gb { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/gb.svg'); }
.fi-ca { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/ca.svg'); }
.fi-fr { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/fr.svg'); }
.fi-de { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/de.svg'); }
.fi-it { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/it.svg'); }
.fi-es { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/es.svg'); }
.fi-ru { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/ru.svg'); }
.fi-jp { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/jp.svg'); }
.fi-cn { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/cn.svg'); }
.fi-in { background-image: url('https://cdn.jsdelivr.net/gh/lipis/flag-icons/flags/4x3/in.svg'); }
</style>
