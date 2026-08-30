<script setup lang="ts">
/**
 * @description
 * Labelled text input in the panel's dark theme, with an optional leading icon.
 *
 * A bare `<input>` carries no stylesheet of its own on a dark background, so every
 * screen that hand-rolled one ended up pasting the same twelve utility classes. This
 * component is that paste, once.
 */
import { computed, useSlots } from 'vue'

/** Two-way bound field value. */
const model = defineModel<string>({ required: true })

const props = withDefaults(defineProps<{
  /** Text shown above the field. */
  label: string
  /** Native input type — 'text', 'email', 'password', 'tel'. */
  type?: string
  /** Greyed hint shown while the field is empty. */
  placeholder?: string
  /** Value for the native `autocomplete` attribute, so password managers behave. */
  autocomplete?: string
  /** Whether the browser should block submission while empty. */
  required?: boolean
  /** Native `inputmode`, e.g. 'numeric' for a TOTP code. */
  inputmode?: 'text' | 'numeric'
  /** Maximum accepted length, omitted when unbounded. */
  maxlength?: number
  /** Whether the field takes focus when it mounts. */
  autofocus?: boolean
  /** Disables the field, e.g. while a request is in flight. */
  disabled?: boolean
}>(), {
  type: 'text',
  placeholder: '',
  autocomplete: undefined,
  required: false,
  inputmode: 'text',
  maxlength: undefined,
  autofocus: false,
  disabled: false,
})

const slots = useSlots()

/** Whether a leading icon is present, which decides the input's left padding. */
const hasIcon = computed(() => slots.icon !== undefined)
</script>

<template>
  <div class="flex flex-col gap-1.5">
    <label class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted">
      {{ props.label }}
    </label>
    <div class="relative">
      <span
        v-if="hasIcon"
        class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-muted flex items-center"
      >
        <slot name="icon" />
      </span>
      <input
        v-model="model"
        :type="props.type"
        :placeholder="props.placeholder"
        :autocomplete="props.autocomplete"
        :required="props.required"
        :inputmode="props.inputmode"
        :maxlength="props.maxlength"
        :autofocus="props.autofocus"
        :disabled="props.disabled"
        class="w-full bg-white/[0.04] border border-white/[0.08] rounded-[10px] pr-3 py-2.5 text-sm text-text-primary placeholder:text-text-muted outline-none transition-all duration-200 focus:border-primary-500/50 focus:bg-primary-500/[0.04] focus:ring-2 focus:ring-primary-500/10 disabled:opacity-50"
        :class="hasIcon ? 'pl-9' : 'pl-3'"
      >
    </div>
  </div>
</template>
