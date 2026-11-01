<script setup lang="ts">
/**
 * Phone field with a country-code picker.
 *
 * Driven by the `phoneNumber` / `countryCode` pair (it never reads `modelValue`). The picker lists
 * every country of the geo atlas in a searchable listbox: the field starts empty and filters by
 * name, native name, ISO code and calling code; Arrow keys move the highlight, Enter chooses and
 * Escape closes. The flag of each country is the emoji the atlas carries for it, so the component
 * loads no image from a host.
 */
import { computed, nextTick, onMounted, onUnmounted, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiPanelTransition from '@/components/ui/UiPanelTransition.vue'
import { useGeoOptions } from '@/composables/useGeoOptions'

/** Props for UiPhoneInput. */
const props = defineProps<{
  /**
   * Combined phone value.
   *
   * Optional: this component is driven by the `phoneNumber`/`countryCode` pair and never
   * reads `modelValue`, so a call site binding only the two fields stays valid.
   */
  modelValue?: string | null
  /** The national number (two-way bound with `v-model:phone-number`). */
  phoneNumber?: string | null
  /** ISO code of the selected country (two-way bound with `v-model:country-code`). */
  countryCode?: string
  /** Not used: the hint comes from the selected country. Kept for call-site compatibility. */
  placeholder?: string
}>()

const emit = defineEmits<{
  /** Reserved for the combined value; never emitted today. */
  'update:modelValue': [value: string | null]
  /** Emitted with the national number as the user types. */
  'update:phoneNumber': [value: string | null]
  /** Emitted with the ISO code of the country the user picks. */
  'update:countryCode': [value: string]
}>()

/** Translator for the strings this component builds itself. */
const { t } = useI18n()

/** One row of the country list. */
interface PhoneCountry {
  /** ISO 3166-1 alpha-2 code. */
  value: string
  /** English name. */
  label: string
  /** Name in the country's own language; empty when it equals the English one. */
  native: string
  /** Flag emoji. */
  flag: string
  /** Calling code with the plus sign. */
  code: string
}

/** Height the open panel needs, in pixels; below this much room under the trigger, the panel flips above. */
const PANEL_HEIGHT = 340

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

/** Country list of the geo atlas: the name, native name, flag and dialling code of each country come from here. */
const { geoCountries, resolvePhoneCode } = useGeoOptions()

/** Every country that has a calling code, sorted by name. */
const phoneCountries: PhoneCountry[] = geoCountries
  .filter((c) => c.iso2 && c.name && c.phone)
  .map((c) => ({
    value: String(c.iso2),
    label: String(c.name),
    native: c.native && c.native !== c.name ? String(c.native) : '',
    flag: c.emoji ?? '',
    code: resolvePhoneCode(String(c.iso2)),
  }))
  .sort((a, b) => a.label.localeCompare(b.label))

/** Generated id of the listbox, also the prefix of every option id. */
const listId = `admin-phone-countries-${useId()}`

/** Root element, used to tell an inside click from an outside one. */
const rootRef = ref<HTMLElement | null>(null)

/** The trigger button, which gets focus back when the panel closes by keyboard. */
const triggerRef = ref<HTMLButtonElement | null>(null)

/** The search field, focused when the panel opens. */
const searchRef = ref<HTMLInputElement | null>(null)

/** The listbox, scrolled to keep the highlighted row visible. */
const listRef = ref<HTMLElement | null>(null)

/** Whether the country list is open. */
const isOpen = ref(false)

/** Whether the panel sits above the trigger because there was no room below. */
const placeAbove = ref(false)

/** The search text; empty every time the list opens. */
const query = ref('')

/** Index, in the visible list, of the highlighted row. */
const activeIndex = ref(0)

/** ISO code of the selected country (the default when none is given). */
const selectedCountryCode = computed<string>(() => props.countryCode || DEFAULT_COUNTRY)

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

/** The selected country entry. */
const selectedCountry = computed(() =>
  phoneCountries.find((c) => c.value === selectedCountryCode.value),
)

/** Sample number of the selected country, shown as the field hint. */
const currentPlaceholder = computed<string>(() => SAMPLE_NUMBERS[selectedCountryCode.value] ?? t('shared.phoneNumber2'))

/**
 * Countries matching the search text.
 *
 * A query of digits (with an optional plus) matches the start of a calling code; any other query
 * matches inside the name or native name, or the start of the ISO code.
 */
const visibleCountries = computed<PhoneCountry[]>(() => {
  const term = query.value.trim().toLowerCase()
  if (!term) {
    return phoneCountries
  }
  const digits = term.replace(/^\+/, '')
  if (/^\d+$/.test(digits)) {
    return phoneCountries.filter((c) => c.code.slice(1).startsWith(digits))
  }
  return phoneCountries.filter((c) =>
    c.label.toLowerCase().includes(term)
    || c.native.toLowerCase().includes(term)
    || c.value.toLowerCase().startsWith(term),
  )
})

/** Id of the highlighted option, for the combobox's `aria-activedescendant`. */
const activeOptionId = computed<string | undefined>(() => {
  const country = visibleCountries.value[activeIndex.value]
  return country ? `${listId}-${country.value}` : undefined
})

/** Class list of the panel: above or below the trigger. */
const panelClasses = computed<string>(() => [
  'absolute left-0 z-50 w-full min-w-[19rem] bg-surface-card border border-border rounded-[10px] shadow-xl overflow-hidden',
  placeAbove.value ? 'bottom-full mb-1.5' : 'top-full mt-1.5',
].join(' '))

/**
 * Class list of one country row for its state.
 *
 * @param code - ISO code of the row's country.
 * @param index - Position of the row in the visible list.
 * @returns The row classes: chosen, highlighted, or plain.
 */
const rowClasses = (code: string, index: number): string => {
  const base = 'flex items-center gap-3 px-3 py-2 text-[0.82rem] cursor-pointer select-none transition-colors'
  if (code === selectedCountryCode.value) {
    return `${base} bg-primary-600/15 text-text-primary`
  }
  if (index === activeIndex.value) {
    return `${base} bg-surface-hover text-text-primary`
  }
  return `${base} text-text-secondary`
}

/** Decides whether the panel opens below or above the trigger, from the room the viewport has. */
const measurePlacement = (): void => {
  const rect = triggerRef.value?.getBoundingClientRect()
  if (!rect) {
    return
  }
  const below = window.innerHeight - rect.bottom
  placeAbove.value = below < PANEL_HEIGHT && rect.top > below
}

/** Keeps the highlighted row inside the visible part of the list. */
const revealActive = (): void => {
  void nextTick(() => {
    listRef.value?.querySelectorAll('[role="option"]')[activeIndex.value]?.scrollIntoView?.({ block: 'nearest' })
  })
}

/** Opens the list with an empty search and the selected country highlighted. */
const openList = (): void => {
  if (isOpen.value) {
    return
  }
  query.value = ''
  activeIndex.value = Math.max(0, phoneCountries.findIndex((c) => c.value === selectedCountryCode.value))
  measurePlacement()
  isOpen.value = true
  revealActive()
  void nextTick(() => searchRef.value?.focus())
}

/** Closes the list. */
const close = (): void => {
  isOpen.value = false
}

/** Closes the list and returns focus to the trigger, for the keyboard path. */
const closeAndFocus = (): void => {
  close()
  triggerRef.value?.focus()
}

/** Opens a closed list and closes an open one. */
const toggle = (): void => {
  if (isOpen.value) {
    close()
    return
  }
  openList()
}

/**
 * Moves the highlight, stopping at both ends of the visible list.
 *
 * @param step - Rows to move: 1 down, -1 up.
 */
const move = (step: number): void => {
  const last = visibleCountries.value.length - 1
  if (last < 0) {
    return
  }
  activeIndex.value = Math.min(last, Math.max(0, activeIndex.value + step))
  revealActive()
}

/**
 * Picks a country and closes the list.
 *
 * @param code - ISO code of the chosen country.
 */
const selectCountry = (code: string): void => {
  emit('update:countryCode', code)
  closeAndFocus()
}

/** Picks the highlighted row, if the search left any. */
const chooseActive = (): void => {
  const country = visibleCountries.value[activeIndex.value]
  if (country) {
    selectCountry(country.value)
  }
}

/** A new search starts from the first match. */
watch(query, () => {
  activeIndex.value = 0
})

/**
 * Closes the list when the pointer goes down outside the component.
 *
 * @param event - The mouse event.
 */
const onOutsideClick = (event: MouseEvent): void => {
  if (rootRef.value && !rootRef.value.contains(event.target as Node)) { // target of a document mousedown is always a Node
    close()
  }
}

onMounted(() => document.addEventListener('mousedown', onOutsideClick))
onUnmounted(() => document.removeEventListener('mousedown', onOutsideClick))
</script>

<template>
  <div ref="rootRef" class="relative flex items-center gap-0 w-full">
    <!-- Country code button (dropdown trigger) -->
    <button
      ref="triggerRef"
      type="button"
      data-testid="phone-country-trigger"
      class="flex items-center gap-2 px-3 py-2 bg-surface-field border border-border border-r-0 rounded-l-[9px] cursor-pointer hover:bg-surface-hover outline-none focus-visible:border-primary-500/50 focus-visible:ring-2 focus-visible:ring-primary-500/30 transition-colors whitespace-nowrap"
      aria-haspopup="listbox"
      :aria-expanded="isOpen"
      :aria-controls="isOpen ? listId : undefined"
      :aria-label="`${t('ui.phoneInput.countryCode')}: ${selectedCountry?.code ?? ''}`"
      @click="toggle"
      @keydown.arrow-down.prevent="openList"
      @keydown.arrow-up.prevent="openList"
    >
      <span class="w-5 text-center" aria-hidden="true">{{ selectedCountry?.flag }}</span>
      <span class="text-[0.82rem] font-medium text-text-primary tabular-nums">{{ selectedCountry?.code }}</span>
      <svg
        class="w-4 h-4 text-text-secondary shrink-0 transition-transform duration-150"
        :class="isOpen ? 'rotate-180' : ''"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        aria-hidden="true"
      >
        <polyline points="6 9 12 15 18 9" />
      </svg>
    </button>

    <!-- Phone number input -->
    <input
      v-model="phoneNumberModel"
      type="text"
      :placeholder="currentPlaceholder"
      data-testid="phone-number-input"
      class="flex-1 min-w-0 px-3 py-2 text-[0.82rem] text-text-primary bg-surface-field border border-border border-l-0 rounded-r-[9px] placeholder:text-text-muted outline-none focus:border-primary-500/50 focus:ring-2 focus:ring-primary-500/10 transition-colors"
    >

    <!-- Country list: search field and listbox, anchored to the whole row -->
    <UiPanelTransition>
      <div v-if="isOpen" :class="panelClasses" data-testid="phone-country-panel">
        <div class="p-1.5 border-b border-border">
          <input
            ref="searchRef"
            v-model="query"
            type="text"
            role="combobox"
            autocomplete="off"
            :placeholder="t('ui.phoneInput.searchCountry')"
            :aria-label="t('ui.phoneInput.searchCountry')"
            aria-expanded="true"
            :aria-controls="listId"
            :aria-activedescendant="activeOptionId"
            data-testid="phone-country-search"
            class="w-full bg-surface-field border border-border rounded-lg px-2.5 py-1.5 text-[0.82rem] text-text-primary placeholder:text-text-muted outline-none focus:border-primary-500/50 focus:ring-2 focus:ring-primary-500/20 transition-colors"
            @keydown.arrow-down.prevent="move(1)"
            @keydown.arrow-up.prevent="move(-1)"
            @keydown.enter.prevent="chooseActive"
            @keydown.escape.prevent.stop="closeAndFocus"
            @keydown.tab="close"
          >
        </div>

        <div
          :id="listId"
          ref="listRef"
          role="listbox"
          :aria-label="t('ui.phoneInput.countryCode')"
          class="max-h-56 overflow-y-auto overscroll-contain py-1"
        >
          <div
            v-for="(country, index) in visibleCountries"
            :id="`${listId}-${country.value}`"
            :key="country.value"
            role="option"
            :aria-selected="selectedCountryCode === country.value"
            :data-testid="`phone-country-${country.value}`"
            :class="rowClasses(country.value, index)"
            @click="selectCountry(country.value)"
            @mousemove="activeIndex = index"
          >
            <span class="w-5 text-center shrink-0" aria-hidden="true">{{ country.flag }}</span>
            <span class="min-w-0 flex-1 truncate">
              <span class="text-text-primary">{{ country.label }}</span>
              <span v-if="country.native" class="ml-2 text-text-muted">{{ country.native }}</span>
            </span>
            <span class="shrink-0 text-text-secondary tabular-nums">{{ country.code }}</span>
            <span class="w-3.5 h-3.5 shrink-0">
              <svg
                v-if="selectedCountryCode === country.value"
                class="w-3.5 h-3.5 text-primary-500"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2.5"
                aria-hidden="true"
              >
                <polyline points="20 6 9 17 4 12" />
              </svg>
            </span>
          </div>
          <p
            v-if="visibleCountries.length === 0"
            class="px-3 py-2 text-[0.82rem] text-text-secondary text-center"
            data-testid="phone-country-empty"
          >
            {{ t('ui.uiSelect.noResults') }}
          </p>
        </div>
      </div>
    </UiPanelTransition>
  </div>
</template>
