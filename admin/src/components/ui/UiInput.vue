<script setup lang="ts">
/**
 * Single-line text field.
 *
 * Draws the themed field (surface, border, focus ring, invalid state) from tokens, so it follows
 * light/dark. With a `label` or an `icon` slot it also renders the caption and the leading icon;
 * otherwise the native `<input>` is the root. Attributes (`class`, `data-testid`, `maxlength`,
 * `min`, `readonly`, `autocomplete` and the rest) always land on the `<input>`; an email, password,
 * tel or url field gets a matching `autocomplete` unless the screen passes its own. A password field
 * carries a show/hide button (`aria-pressed`) that flips the input between hidden and plain text.
 * Emits the raw string, so `v-model.number` and `.trim` convert through Vue's own modifier handling.
 */
import { computed, ref, useAttrs, useSlots } from 'vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import { translate } from '@/lib/translate'
import {
  fieldBaseClasses,
  fieldInvalidClasses,
  fieldWidthClasses,
  focusRingClasses,
  inputAutocomplete,
  UiInputType,
} from '@/utils/ui/input'
import { fieldSizeClasses } from '@/utils/ui/size'
import type { UiSize } from '@/utils/ui/size'
import { labelVariantClasses } from '@/utils/ui/text'

defineOptions({ inheritAttrs: false })

/** Props for UiInput. */
const props = withDefaults(defineProps<{
  /** Current value (two-way bound with `v-model`). */
  modelValue?: string | number | null
  /**
   * Modifiers of `v-model` (`.number`, `.trim`). Declared so they are not forwarded to the native
   * element as an attribute; Vue applies them itself when the component emits the update.
   */
  modelModifiers?: Record<string, boolean>
  /** Native input type. */
  type?: UiInputType
  /** Padding and type size. */
  size?: UiSize
  /** Caption shown above the field. */
  label?: string
  /** Hint shown while the field is empty. */
  placeholder?: string
  /** Marks the value as refused by the server (red border, `aria-invalid`). */
  invalid?: boolean
}>(), {
  modelValue: '',
  modelModifiers: undefined,
  type: 'text',
  size: 'md',
  label: undefined,
  placeholder: undefined,
  invalid: false,
})

const emit = defineEmits<{
  /** Emitted with the new text on every keystroke. */
  'update:modelValue': [value: string]
}>()

/** Attributes forwarded to the native input. */
const attrs = useAttrs()

/** Slots, to detect the optional leading icon. */
const slots = useSlots()

/** Whether a leading icon is present, which decides the left padding. */
const hasIcon = computed<boolean>(() => slots.icon !== undefined)

/** Whether this is a password field, which gets the show/hide button. */
const isPassword = computed<boolean>(() => props.type === UiInputType.Password)

/** Whether the password is currently shown as plain text. */
const revealed = ref(false)

/** The type given to the native input: plain text while a password is revealed. */
const nativeType = computed<UiInputType>(() => (isPassword.value && revealed.value ? UiInputType.Text : props.type))

/** Accessible name of the show/hide button. */
const revealLabel = computed<string>(() => (revealed.value ? translate('ui.input.hidePassword') : translate('ui.input.showPassword')))

/** Autofill hint for the field's type; the screen's own `autocomplete` attribute overrides it. */
const autocomplete = computed<string | undefined>(() => inputAutocomplete[props.type])

/** Whether the label/icon/button wrapper is rendered around the input. */
const wrapped = computed<boolean>(() => props.label !== undefined || hasIcon.value || isPassword.value)

/** Classes of the show/hide button. */
const revealClasses = `absolute right-1.5 top-1/2 -translate-y-1/2 flex h-7 w-7 items-center justify-center rounded-lg text-text-secondary hover:text-text-primary hover:bg-surface-hover transition-colors cursor-pointer ${focusRingClasses}`

/** Flips the password between hidden and plain text. */
const toggleReveal = (): void => {
  revealed.value = !revealed.value
}

/** Field classes: the themed box, the size, the invalid state and room for the icon. */
const fieldClasses = computed<string[]>(() => [
  fieldWidthClasses,
  fieldBaseClasses,
  fieldSizeClasses[props.size],
  props.invalid ? fieldInvalidClasses : '',
  hasIcon.value ? 'pl-9' : '',
  isPassword.value ? 'pr-10' : '',
])

/**
 * Forwards the native input event as a model update.
 *
 * @param event - Native input event.
 */
const onInput = (event: Event): void => {
  emit('update:modelValue', (event.target as HTMLInputElement).value)
}
</script>

<template>
  <div v-if="wrapped" class="flex flex-col gap-1.5">
    <span
      v-if="label !== undefined"
      :class="labelVariantClasses.field"
    >
      {{ label }}
    </span>
    <div class="relative">
      <span
        v-if="hasIcon"
        class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-secondary flex items-center"
      >
        <slot name="icon" />
      </span>
      <input
        :autocomplete="autocomplete"
        v-bind="attrs"
        :type="nativeType"
        :value="modelValue ?? ''"
        :placeholder="placeholder"
        :aria-label="label"
        :aria-invalid="invalid || undefined"
        :class="fieldClasses"
        @input="onInput"
      >
      <button
        v-if="isPassword"
        type="button"
        data-testid="input-toggle-password"
        :aria-label="revealLabel"
        :aria-pressed="revealed"
        :title="revealLabel"
        :class="revealClasses"
        @click="toggleReveal"
      >
        <UiIcon :markup="revealed ? Icon.EyeOff : Icon.Eye" />
      </button>
    </div>
  </div>
  <input
    v-else
    :autocomplete="autocomplete"
    v-bind="attrs"
    :type="nativeType"
    :value="modelValue ?? ''"
    :placeholder="placeholder"
    :aria-invalid="invalid || undefined"
    :class="fieldClasses"
    @input="onInput"
  >
</template>
