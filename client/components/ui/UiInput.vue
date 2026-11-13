<template>
  <div class="w-full">
    <label v-if="label" :for="inputId" class="block text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-600 dark:text-primary-400">*</span>
    </label>

    <div class="relative">
      <!-- Prefix slot (e.g. search icon) -->
      <div v-if="$slots.prefix" class="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none text-gray-500 dark:text-gray-400">
        <slot name="prefix" />
      </div>

      <Icon
        v-else-if="icon"
        :name="icon"
        class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500 dark:text-gray-400 pointer-events-none"
      />

      <input
        :id="inputId"
        :name="name"
        :type="resolvedType"
        :value="modelValue"
        :placeholder="placeholder"
        :required="required"
        :disabled="disabled"
        :autocomplete="autocomplete"
        :aria-invalid="error ? 'true' : undefined"
        :data-testid="testid"
        :class="inputClasses"
        @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
        @blur="$emit('blur')"
        @focus="$emit('focus')"
      >

      <button
        v-if="isPassword"
        type="button"
        tabindex="-1"
        :data-testid="testid ? `${testid}-toggle` : 'ui-input-toggle'"
        :aria-label="showPassword ? $t('ui.hidePassword') : $t('ui.showPassword')"
        :class="toggleClasses"
        @click="showPassword = !showPassword"
      >
        <Eye v-if="!showPassword" :size="18" :stroke-width="2" />
        <EyeOff v-else :size="18" :stroke-width="2" />
      </button>

      <AlertCircle
        v-else-if="error"
        :size="20"
        :stroke-width="2"
        class="absolute right-3 top-1/2 -translate-y-1/2 text-red-500"
      />
    </div>

    <p v-if="error" class="mt-2 text-sm text-red-600 dark:text-red-400">{{ error }}</p>
    <p v-else-if="hint" class="mt-2 text-sm text-gray-500 dark:text-gray-400">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
/**
 * UiInput: a labelled single-line text field with error and hint lines.
 *
 * Owns the field frame (via the shared `fieldStateClasses`), the password reveal toggle and the
 * left icon/prefix. `data-testid` placed on the component lands on the wrapper; `testid` lands on
 * the `<input>` itself, which is what a test types into.
 */
import { computed, ref, useId, useSlots } from 'vue'
import { AlertCircle, Eye, EyeOff } from 'lucide-vue-next'
import { UiInputType, UiSize } from '~/utils/ui/enums'
import { CONTROL_SIZE_CLASSES, FOCUS_RING, fieldStateClasses } from '~/utils/ui/tones'

/** Props of UiInput. */
interface Props {
  /** DOM id; generated when omitted so the label is always bound. */
  id?: string
  /** Label text above the field. */
  label?: string
  /** Kind of text the field takes. */
  type?: `${UiInputType}`
  /** Current value (v-model). */
  modelValue?: string | number
  /** Hint shown inside the empty field; every text field must set one. */
  placeholder?: string
  /** Marks the field required and adds the asterisk. */
  required?: boolean
  /** Disables the field. */
  disabled?: boolean
  /** Error line; also switches the field to its error look. */
  error?: string
  /** Neutral help line under the field, hidden while an error shows. */
  hint?: string
  /** Name of a leading icon (Nuxt Icon). */
  icon?: string
  /** Size step. */
  size?: `${UiSize}`
  /** Browser autocomplete token. */
  autocomplete?: string
  /** Form field name, for a form that posts natively. */
  name?: string
  /** `data-testid` of the inner input element. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  label: undefined,
  type: UiInputType.Text,
  modelValue: undefined,
  placeholder: undefined,
  required: false,
  disabled: false,
  error: undefined,
  hint: undefined,
  icon: undefined,
  size: UiSize.Md,
  autocomplete: undefined,
  name: undefined,
  testid: undefined,
})

defineEmits<{
  /** The user changed the value. */
  'update:modelValue': [value: string]
  /** The field lost focus. */
  blur: []
  /** The field gained focus. */
  focus: []
}>()

/** The slots this input was given, read below to decide whether the field needs left padding. */
const slots = useSlots()

/** Generated id used when the caller passes none. */
const generatedId = useId()

/** The id bound to both the label and the input. */
const inputId = computed(() => props.id ?? generatedId)

/** Whether a password field currently shows its text. */
const showPassword = ref(false)

/** Whether this is a password field (it gets the reveal toggle). */
const isPassword = computed(() => props.type === UiInputType.Password)

/** Native input type after applying the password reveal toggle. */
const resolvedType = computed(() =>
  isPassword.value && showPassword.value ? UiInputType.Text : props.type,
)

/** Class list of the password reveal button: muted icon, one hover step, the kit focus ring. */
const toggleClasses = `absolute right-3 top-1/2 -translate-y-1/2 rounded text-gray-500 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200 transition-colors ${FOCUS_RING}`

/** Class list of the `<input>`. */
const inputClasses = computed(() => {
  // `slots.prefix`, not `$slots` in a prop: the template's `v-if="$slots.prefix"` is right, but
  // the script has to read the same slot through useSlots() or the padding silently disappears.
  const iconPadding = (props.icon || slots.prefix) ? 'pl-10' : ''
  const rightPadding = (isPassword.value || props.error) ? 'pr-10' : ''
  return [
    fieldStateClasses({ error: Boolean(props.error), disabled: props.disabled }),
    'placeholder:text-gray-500 dark:placeholder:text-gray-400',
    CONTROL_SIZE_CLASSES[props.size as UiSize],
    iconPadding,
    rightPadding,
  ].filter(Boolean).join(' ')
})
</script>
