<template>
  <div ref="rootRef" class="shrink-0">
    <!-- Trigger: flag and calling code of the chosen country -->
    <button
      :id="id"
      ref="triggerRef"
      type="button"
      :disabled="disabled"
      :class="triggerClasses"
      aria-haspopup="listbox"
      :aria-expanded="isOpen"
      :aria-controls="isOpen ? listId : undefined"
      :aria-label="`${t('ui.phoneCountry')}: ${dialCode}`"
      :data-testid="testid"
      @click="toggle"
      @keydown.arrow-down.prevent="openList"
      @keydown.arrow-up.prevent="openList"
    >
      <span class="flex h-4 w-6 shrink-0 items-center justify-center" aria-hidden="true">
        <span :class="['vti__flag', flagClass]" />
      </span>
      <span class="text-sm tabular-nums">{{ dialCode }}</span>
      <ChevronDown
        :size="16"
        :stroke-width="2"
        class="shrink-0 text-gray-500 transition-transform duration-200 dark:text-gray-400"
        :class="isOpen ? 'rotate-180' : ''"
        aria-hidden="true"
      />
    </button>

    <!-- Panel: search field and the country listbox; anchored to the nearest positioned ancestor (the whole phone row) -->
    <div
      v-if="isOpen"
      :class="panelClasses"
      data-testid="phone-country-panel"
    >
      <div class="relative border-b border-gray-200 p-2 dark:border-white/10">
        <Search
          :size="16"
          :stroke-width="2"
          class="pointer-events-none absolute left-5 top-1/2 -translate-y-1/2 text-gray-500 dark:text-gray-400"
          aria-hidden="true"
        />
        <input
          ref="searchRef"
          v-model="query"
          type="text"
          role="combobox"
          autocomplete="off"
          :placeholder="t('ui.searchCountry')"
          :aria-label="t('ui.searchCountry')"
          aria-expanded="true"
          :aria-controls="listId"
          :aria-activedescendant="activeOptionId"
          data-testid="phone-country-search"
          class="min-h-9 w-full rounded-md border border-gray-200 bg-transparent py-1.5 pl-9 pr-3 text-sm text-gray-900 placeholder:text-gray-500 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-white/10 dark:text-white dark:placeholder:text-gray-400"
          @keydown.arrow-down.prevent="move(1)"
          @keydown.arrow-up.prevent="move(-1)"
          @keydown.enter.prevent="chooseActive"
          @keydown.escape.prevent.stop="closeAndFocus"
          @keydown.tab="close"
        >
      </div>

      <ul
        :id="listId"
        ref="listRef"
        role="listbox"
        :aria-label="t('ui.phoneCountry')"
        class="max-h-64 overflow-y-auto overscroll-contain py-1"
        data-testid="phone-country-list"
      >
        <li
          v-for="(country, index) in visibleCountries"
          :id="`${listId}-${country.iso}`"
          :key="country.iso"
          role="option"
          :aria-selected="country.iso === modelValue"
          :data-testid="`phone-country-${country.iso}`"
          :class="rowClasses(country.iso, index)"
          @click="choose(country)"
          @mousemove="activeIndex = index"
        >
          <span class="flex h-4 w-6 shrink-0 items-center justify-center" aria-hidden="true">
            <span :class="['vti__flag', country.iso.toLowerCase()]" />
          </span>
          <span class="min-w-0 flex-1 truncate">
            <span>{{ country.name }}</span>
            <span v-if="country.nativeName" class="ml-2 text-gray-500 dark:text-gray-400">{{ country.nativeName }}</span>
          </span>
          <span class="shrink-0 text-gray-500 tabular-nums dark:text-gray-400">{{ country.dialCode }}</span>
          <span class="flex h-4 w-4 shrink-0 items-center justify-center">
            <Check
              v-if="country.iso === modelValue"
              :size="14"
              :stroke-width="2.5"
              class="text-primary-600 dark:text-primary-400"
              aria-hidden="true"
            />
          </span>
        </li>
        <li
          v-if="!visibleCountries.length"
          class="px-4 py-3 text-center text-sm text-gray-500 dark:text-gray-400"
          data-testid="phone-country-empty"
        >
          {{ t('ui.noCountries') }}
        </li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiPhoneCountryPicker. The country part of the phone field: a trigger showing the flag and
 * calling code, and a searchable listbox opened from it.
 *
 * The search field starts empty and filters by country name, native name, ISO code and calling
 * code. It is a combobox over the listbox: Arrow keys move the highlight, Enter chooses, Escape
 * closes and returns focus to the trigger. The panel opens below the trigger and flips above it
 * when there is not room, and its list scrolls past a fixed height. It is positioned against the
 * nearest positioned ancestor, so the phone row (not the narrow trigger) sets its width. Flags are
 * the sprite the tel-input stylesheet ships, so they look the same on every platform.
 */
import { computed, nextTick, ref, shallowRef, useId, watch } from 'vue'
import { Check, ChevronDown, Search } from 'lucide-vue-next'
import { onClickOutside } from '@vueuse/core'
import 'vue-tel-input/vue-tel-input.css'
import { dialCodeOf, filterPhoneCountries, listPhoneCountries } from '~/utils/phoneCountries'
import type { PhoneCountry } from '~/utils/phoneCountries'
import { FOCUS_RING, fieldStateClasses } from '~/utils/ui/tones'

/** Props of UiPhoneCountryPicker. */
interface Props {
  /** ISO code of the chosen country (v-model). */
  modelValue: string
  /** DOM id of the trigger. */
  id?: string
  /** Disables the picker. */
  disabled?: boolean
  /** Whether the field next to it shows an error, so the trigger takes the error look too. */
  error?: boolean
  /** `data-testid` of the trigger. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  disabled: false,
  error: false,
  testid: undefined,
})

/** Events this component raises. */
const emit = defineEmits<{
  /** The reader chose a country. */
  'update:modelValue': [iso: string]
}>()

/** Translator and the active language, which names the countries. */
const { t, locale } = useI18n()

/** Height the open panel needs, in pixels; below this much room under the trigger, the panel flips above. */
const PANEL_HEIGHT = 340

/** Generated id of the listbox, also the prefix of every option id. */
const listId = `phone-countries-${useId()}`

/** Root element, used to tell an inside click from an outside one. */
const rootRef = ref<HTMLElement | null>(null)

/** The trigger button, which gets focus back when the panel closes by keyboard. */
const triggerRef = ref<HTMLButtonElement | null>(null)

/** The search field, focused when the panel opens. */
const searchRef = ref<HTMLInputElement | null>(null)

/** The listbox, scrolled to keep the highlighted row visible. */
const listRef = ref<HTMLElement | null>(null)

/** Whether the panel is open. */
const isOpen = ref(false)

/** Whether the panel sits above the trigger because there was no room below. */
const placeAbove = ref(false)

/** The search text; empty every time the panel opens. */
const query = ref('')

/** Index, in the visible list, of the highlighted row. */
const activeIndex = ref(0)

/** The full country list for the active language; built when the panel opens. */
const countries = shallowRef<PhoneCountry[]>([])

/** Calling code shown on the trigger. */
const dialCode = computed<string>(() => dialCodeOf(props.modelValue))

/** Class of the flag sprite for the chosen country. */
const flagClass = computed<string>(() => props.modelValue.toLowerCase())

/** Countries matching the search text. */
const visibleCountries = computed<PhoneCountry[]>(() => filterPhoneCountries(countries.value, query.value))

/** Id of the highlighted option, for the combobox's `aria-activedescendant`. */
const activeOptionId = computed<string | undefined>(() => {
  const country = visibleCountries.value[activeIndex.value]
  return country ? `${listId}-${country.iso}` : undefined
})

/** Class list of the trigger: the kit's field frame with a flag, code and caret laid in a row. */
const triggerClasses = computed(() => [
  fieldStateClasses({ error: props.error, disabled: props.disabled, open: isOpen.value }),
  FOCUS_RING,
  // The field frame is a full-width block; the trigger is a row of three parts that is only as wide as they are.
  '!flex !w-auto min-h-11 cursor-pointer items-center gap-2 px-3 py-2 whitespace-nowrap',
].join(' '))

/** Class list of the panel: the popover surface, above or below the trigger. */
const panelClasses = computed(() => [
  'absolute left-0 z-50 w-full min-w-[19rem] overflow-hidden rounded-xl border border-gray-200 bg-ui-popover shadow-xl shadow-black/10 dark:border-white/15 dark:shadow-black/40',
  placeAbove.value ? 'bottom-full mb-1.5' : 'top-full mt-1.5',
].join(' '))

/**
 * Class list of one country row for its state.
 *
 * @param iso ISO code of the row's country.
 * @param index Position of the row in the visible list.
 * @returns The row classes: chosen, highlighted, or plain.
 */
const rowClasses = (iso: string, index: number): string => {
  const base = 'flex cursor-pointer select-none items-center gap-3 px-3 py-2 text-sm font-normal transition-colors duration-100'
  if (iso === props.modelValue) {
    return `${base} bg-primary-50 text-gray-900 dark:bg-primary-500/20 dark:text-white`
  }
  if (index === activeIndex.value) {
    return `${base} bg-gray-100 text-gray-900 dark:bg-white/10 dark:text-white`
  }
  return `${base} text-gray-700 dark:text-gray-300`
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

/** Opens the panel with an empty search and the chosen country highlighted. */
const openList = (): void => {
  if (props.disabled || isOpen.value) {
    return
  }
  countries.value = listPhoneCountries(locale.value)
  query.value = ''
  activeIndex.value = Math.max(0, countries.value.findIndex((country) => country.iso === props.modelValue))
  measurePlacement()
  isOpen.value = true
  revealActive()
  void nextTick(() => searchRef.value?.focus())
}

/** Closes the panel. */
const close = (): void => {
  isOpen.value = false
}

/** Closes the panel and returns focus to the trigger, for the keyboard path. */
const closeAndFocus = (): void => {
  close()
  triggerRef.value?.focus()
}

/** Opens a closed panel and closes an open one. */
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
 * @param step Rows to move: 1 down, -1 up.
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
 * Chooses a country and closes the panel.
 *
 * @param country The chosen row.
 */
const choose = (country: PhoneCountry): void => {
  emit('update:modelValue', country.iso)
  closeAndFocus()
}

/** Chooses the highlighted row, if the search left any. */
const chooseActive = (): void => {
  const country = visibleCountries.value[activeIndex.value]
  if (country) {
    choose(country)
  }
}

/** A new search starts from the first match. */
watch(query, () => {
  activeIndex.value = 0
})

onClickOutside(rootRef, close)
</script>
