<template>
  <div ref="wrapperRef" class="relative w-full">
    <label v-if="label" class="block text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-600 dark:text-primary-400">*</span>
    </label>

    <!-- Trigger -->
    <button
      :id="id"
      type="button"
      :disabled="disabled"
      :class="triggerClasses"
      :aria-haspopup="'listbox'"
      :aria-expanded="isOpen"
      :data-testid="testid"
      @click="toggleDropdown"
    >
      <Icon
        v-if="icon"
        :name="icon"
        class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-600 dark:text-gray-400"
      />

      <span v-if="selectedOption" class="block truncate text-left">
        {{ selectedOption.label }}
      </span>
      <span v-else class="block truncate text-left text-gray-500 dark:text-gray-400">
        {{ placeholder ?? $t('ui.selectPlaceholder') }}
      </span>

      <ChevronDown
        :size="20"
        :stroke-width="2"
        class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-600 dark:text-gray-400 transition-transform duration-200"
        :class="{ 'rotate-180': isOpen }"
      />
    </button>

    <p v-if="error" class="mt-2 text-sm text-red-600 dark:text-red-400">
      {{ error }}
    </p>
    <p v-else-if="hint" class="mt-2 text-sm text-gray-500 dark:text-gray-400">
      {{ hint }}
    </p>

    <!-- Menu -->
    <Transition
      enter-active-class="transition-all duration-200 ease-out"
      enter-from-class="opacity-0 -translate-y-2.5"
      leave-active-class="transition-all duration-200 ease-in"
      leave-to-class="opacity-0 -translate-y-1"
    >
      <div
        v-show="isOpen"
        class="absolute z-50 mt-2 w-full rounded-xl bg-ui-popover border-2 border-gray-300 dark:border-gray-700 shadow-xl shadow-black/10 dark:shadow-black/50 overflow-hidden backdrop-blur-xl"
      >
        <div class="max-h-60 overflow-y-auto [&::-webkit-scrollbar]:w-2 [&::-webkit-scrollbar-track]:rounded [&::-webkit-scrollbar-track]:bg-gray-100 dark:[&::-webkit-scrollbar-track]:bg-white/5 [&::-webkit-scrollbar-thumb]:rounded [&::-webkit-scrollbar-thumb]:bg-gradient-to-b [&::-webkit-scrollbar-thumb]:from-primary-500 [&::-webkit-scrollbar-thumb]:to-secondary-500">
          <button
            v-for="option in options"
            :key="option.value"
            type="button"
            :data-testid="`ui-dropdown-option-${option.value}`"
            :class="[
              'w-full px-4 py-3 text-left text-sm transition-all duration-200 flex items-center justify-between group focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500',
              modelValue === option.value
                ? 'bg-gradient-to-r from-primary-500 to-secondary-500 text-on-primary font-semibold'
                : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 hover:text-gray-900 dark:hover:text-on-primary'
            ]"
            @click="selectOption(option)"
          >
            <span>{{ option.label }}</span>
            <Check
              v-if="modelValue === option.value"
              :size="18"
              :stroke-width="2"
            />
          </button>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
/**
 * UiDropdown: a bordered single-choice dropdown (the "heavy" sibling of UiSelect).
 *
 * Same value contract as UiSelect; differs in look (thick border, gradient selected row) and in
 * keeping its menu mounted (`v-show`) so a long list does not re-render on every open.
 */
import { computed, ref } from 'vue'
import { ChevronDown, Check } from 'lucide-vue-next'
import { onClickOutside } from '@vueuse/core'
import { UiSize } from '~/utils/ui/enums'
import { CONTROL_SIZE_CLASSES } from '~/utils/ui/tones'

/** One choice of the menu. */
interface DropdownOption {
  /** Text shown to the reader. */
  label: string
  /** Value emitted when chosen. */
  value: string | number
}

/** Props of UiDropdown. */
interface Props {
  /** DOM id of the trigger. */
  id?: string
  /** Label text above the trigger. */
  label?: string
  /** Selected value (v-model). */
  modelValue?: string | number
  /** Text shown while nothing is selected. */
  placeholder?: string
  /** The choices. */
  options: DropdownOption[]
  /** Marks the field required and adds the asterisk. */
  required?: boolean
  /** Disables the dropdown. */
  disabled?: boolean
  /** Error line; also switches to the error look. */
  error?: string
  /** Neutral help line, hidden while an error shows. */
  hint?: string
  /** Name of a leading icon (Nuxt Icon). */
  icon?: string
  /** Size step. */
  size?: `${UiSize}`
  /** `data-testid` of the trigger button. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  label: undefined,
  modelValue: undefined,
  placeholder: undefined,
  required: false,
  disabled: false,
  error: undefined,
  hint: undefined,
  icon: undefined,
  size: UiSize.Md,
  testid: undefined,
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** The reader picked an option. */
  'update:modelValue': [value: string | number]
}>()

/** Whether the menu is showing. */
const isOpen = ref(false)

/** The wrapper, used to detect clicks outside. */
const wrapperRef = ref<HTMLElement | null>(null)

/** The option matching the current value. */
const selectedOption = computed(() => props.options.find((option) => option.value === props.modelValue))

/** Opens or closes the menu unless disabled. */
const toggleDropdown = (): void => {
  if (!props.disabled) {
    isOpen.value = !isOpen.value
  }
}

/** Closes the menu. */
const closeDropdown = (): void => {
  isOpen.value = false
}

/**
 * Emits the chosen option and closes the menu.
 *
 * @param option The option the reader picked.
 */
const selectOption = (option: DropdownOption): void => {
  emit('update:modelValue', option.value)
  closeDropdown()
}

onClickOutside(wrapperRef, closeDropdown)

/** Class list of the trigger button. */
const triggerClasses = computed(() => {
  let state: string
  if (props.error) {
    state = 'border-red-400 dark:border-red-500/50 focus-visible:border-red-500 focus-visible:ring-red-500/50 bg-red-50 dark:bg-red-500/5'
  } else if (isOpen.value) {
    state = 'border-primary-500 ring-2 ring-primary-500/50 bg-gray-100 dark:bg-white/10'
  } else {
    state = 'border-gray-300 dark:border-gray-700 focus-visible:border-primary-500 focus-visible:ring-primary-500/50 bg-white dark:bg-white/5 hover:border-primary-500/50'
  }
  return [
    'relative w-full border-2 rounded-lg transition-all duration-200 focus:outline-none focus-visible:ring-2 text-gray-900 dark:text-white text-left pr-10',
    CONTROL_SIZE_CLASSES[props.size as UiSize],
    props.icon ? 'pl-10' : '',
    state,
    props.disabled ? 'bg-gray-100 dark:bg-gray-800/50 cursor-not-allowed opacity-60' : 'cursor-pointer',
  ].filter(Boolean).join(' ')
})
</script>
