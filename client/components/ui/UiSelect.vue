<template>
  <div class="w-full">
    <label v-if="label" :for="selectId" class="block text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-600 dark:text-primary-400">*</span>
    </label>

    <div ref="wrapperRef" class="relative">
      <!-- Trigger button -->
      <button
        :id="selectId"
        type="button"
        :disabled="disabled"
        :class="triggerClasses"
        :aria-haspopup="'listbox'"
        :aria-expanded="isOpen"
        :aria-invalid="error ? 'true' : undefined"
        :data-testid="testid"
        @click="toggle"
        @keydown.escape="close"
        @keydown.enter.prevent="toggle"
        @keydown.space.prevent="toggle"
        @keydown.arrow-down.prevent="open"
        @keydown.arrow-up.prevent="open"
      >
        <Icon v-if="icon" :name="icon" class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-600 dark:text-gray-400 pointer-events-none z-10" />
        <span :class="selectedOption ? 'text-gray-900 dark:text-white' : 'text-gray-500 dark:text-gray-400'">
          {{ selectedOption?.label ?? placeholder ?? $t('ui.selectPlaceholder') }}
        </span>
        <ChevronDown
          :size="18"
          :stroke-width="2"
          class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-600 dark:text-gray-400 transition-transform duration-200"
          :class="isOpen ? 'rotate-180' : ''"
        />
      </button>

      <!-- Dropdown list -->
      <Transition
        enter-active-class="transition duration-150 ease-out"
        enter-from-class="opacity-0 translate-y-1 scale-95"
        enter-to-class="opacity-100 translate-y-0 scale-100"
        leave-active-class="transition duration-100 ease-in"
        leave-from-class="opacity-100 translate-y-0 scale-100"
        leave-to-class="opacity-0 translate-y-1 scale-95"
      >
        <ul
          v-if="isOpen"
          role="listbox"
          class="absolute z-50 mt-1.5 w-full rounded-xl border border-gray-200 dark:border-white/15 bg-ui-popover shadow-xl shadow-black/10 dark:shadow-black/40 overflow-hidden py-1"
        >
          <li
            v-for="option in options"
            :key="option.value"
            role="option"
            :aria-selected="modelValue === option.value"
            :data-testid="`ui-select-option-${option.value}`"
            class="flex items-center gap-2 px-4 py-2.5 text-sm cursor-pointer transition-colors duration-150 select-none"
            :class="modelValue === option.value
              ? 'bg-primary-50 dark:bg-primary-500/20 text-primary-600 dark:text-primary-300'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 hover:text-gray-900 dark:hover:text-on-primary'"
            @click="select(option)"
          >
            <Check
              v-if="modelValue === option.value"
              :size="14"
              :stroke-width="2.5"
              class="text-primary-600 dark:text-primary-400 flex-shrink-0"
            />
            <span :class="modelValue !== option.value ? 'ml-[22px]' : ''">{{ option.label }}</span>
          </li>
          <li v-if="!options.length" class="px-4 py-2.5 text-sm text-gray-500 dark:text-gray-400 text-center">
            {{ $t('ui.noOptions') }}
          </li>
        </ul>
      </Transition>
    </div>

    <p v-if="error" class="mt-2 text-sm text-red-600 dark:text-red-400">{{ error }}</p>
    <p v-else-if="hint" class="mt-2 text-sm text-gray-500 dark:text-gray-400">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
/**
 * UiSelect: a styled single-choice select with a keyboard-operable listbox.
 *
 * Draws its own popup (a native `<select>` cannot be themed), shares the field frame with
 * UiInput through `fieldStateClasses`, and closes on outside click or Escape.
 */
import { computed, ref, useId } from 'vue'
import { ChevronDown, Check } from 'lucide-vue-next'
import { onClickOutside } from '@vueuse/core'
import { UiSize } from '~/utils/ui/enums'
import { CONTROL_SIZE_CLASSES, fieldStateClasses } from '~/utils/ui/tones'

/** One choice of the list. */
interface SelectOption {
  /** Text shown to the reader. */
  label: string
  /** Value emitted when chosen. */
  value: string | number
}

/** Props of UiSelect. */
interface Props {
  /** DOM id of the trigger; generated when omitted. */
  id?: string
  /** Label text above the field. */
  label?: string
  /** Selected value (v-model); `null` is a form that has not picked yet. */
  modelValue?: string | number | null
  /** Text shown while nothing is selected; every select must set one. */
  placeholder?: string
  /** The choices. */
  options: SelectOption[]
  /** Marks the field required and adds the asterisk. */
  required?: boolean
  /** Disables the field. */
  disabled?: boolean
  /** Error line; also switches the field to its error look. */
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

/** Generated id used when the caller passes none. */
const generatedId = useId()

/** The id bound to both the label and the trigger. */
const selectId = computed(() => props.id ?? generatedId)

/** Whether the list is showing. */
const isOpen = ref(false)

/** The wrapper, used to detect clicks outside. */
const wrapperRef = ref<HTMLElement | null>(null)

/** The option matching the current value. */
const selectedOption = computed(() => props.options.find((option) => option.value === props.modelValue) ?? null)

/** Opens or closes the list unless disabled. */
const toggle = (): void => {
  if (props.disabled) {
    return
  }
  isOpen.value = !isOpen.value
}

/** Opens the list unless disabled. */
const open = (): void => {
  if (!props.disabled) {
    isOpen.value = true
  }
}

/** Closes the list. */
const close = (): void => {
  isOpen.value = false
}

/**
 * Emits the chosen option and closes the list.
 *
 * @param option The option the reader picked.
 */
const select = (option: SelectOption): void => {
  emit('update:modelValue', option.value)
  close()
}

onClickOutside(wrapperRef, close)

/** Class list of the trigger button. */
const triggerClasses = computed(() => [
  'relative text-left cursor-pointer pr-10',
  fieldStateClasses({ error: Boolean(props.error), disabled: props.disabled, open: isOpen.value }),
  CONTROL_SIZE_CLASSES[props.size as UiSize],
  props.icon ? 'pl-10' : '',
].filter(Boolean).join(' '))
</script>
