<template>
  <label
    :for="inputId"
    class="ui-option-card group flex min-h-[3.5rem] w-full items-center gap-3 rounded-xl border px-4 py-3 text-left transition-colors duration-150 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary-500 has-[:focus-visible]:ring-offset-2 has-[:focus-visible]:ring-offset-transparent dark:has-[:focus-visible]:ring-primary-400"
    :class="[
      selected ? activeClass : idleClass,
      disabled ? 'cursor-not-allowed opacity-60' : 'cursor-pointer',
    ]"
    :data-state="selected ? 'checked' : 'unchecked'"
    :data-testid="testid"
  >
    <!-- The native radio carries the group semantics, the arrow keys and the form value. -->
    <input
      :id="inputId"
      type="radio"
      :name="name"
      :value="value"
      :checked="selected"
      :disabled="disabled"
      :data-testid="testid ? `${testid}-input` : undefined"
      class="sr-only"
      @change="emit('update:modelValue', value)"
    >

    <!-- Radio indicator: a ring that holds a dot while chosen. -->
    <span
      aria-hidden="true"
      class="ui-option-card-indicator flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-2 border-current"
      :class="selected ? accentClass : mutedClass"
    >
      <span v-if="selected" class="h-2.5 w-2.5 rounded-full bg-current" />
    </span>

    <UiTile
      v-if="icon"
      size="md"
      :icon="icon"
      class="border border-gray-200 bg-white/60 dark:border-white/10 dark:bg-white/5"
      :class="selected ? accentClass : mutedClass"
    />

    <span class="flex min-w-0 flex-1 flex-col">
      <span class="text-sm font-medium leading-snug text-gray-900 dark:text-white">{{ label }}</span>
      <span v-if="description" class="text-xs leading-snug text-gray-500 dark:text-gray-400">{{ description }}</span>
    </span>
  </label>
</template>

<script setup lang="ts">
/**
 * UiOptionCard: one selectable row of a single-choice picker (payment gateway, saved card).
 *
 * The whole row is the click target. A hidden native radio sits inside the label, so the row is
 * keyboard operable with the arrow keys within its `name` group, and a keyboard focus draws a
 * ring around the row. The radio indicator, the optional icon tile and the label with its short
 * description share one vertically centred line.
 *
 * Colours default to the portal's primary accent. A template with its own palette passes the
 * four class props (written as literal Tailwind classes at the call site).
 */
import { computed, useId } from 'vue'
import type { Component } from 'vue'

/** Props of UiOptionCard. */
interface Props {
  /** DOM id of the native radio; generated when omitted. */
  id?: string
  /** Group name shared by the options of one picker. */
  name: string
  /** Value this option stands for. */
  value: string | number
  /** Currently selected value of the group (v-model). */
  modelValue?: string | number | null
  /** Main text of the row. */
  label: string
  /** Short helper text under the label. */
  description?: string
  /** Icon drawn in the tile before the text; no tile when omitted. */
  icon?: Component
  /** Disables the option. */
  disabled?: boolean
  /** `data-testid` of the row; the native radio gets the same id with an `-input` suffix. */
  testid?: string
  /** Classes of the row while chosen (border and background). */
  activeClass?: string
  /** Classes of the row while not chosen (border and background). */
  idleClass?: string
  /** Text colour of the indicator and icon while chosen. */
  accentClass?: string
  /** Text colour of the indicator and icon while not chosen. */
  mutedClass?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  modelValue: undefined,
  description: undefined,
  icon: undefined,
  disabled: false,
  testid: undefined,
  activeClass: 'border-primary-500 bg-primary-500/5 dark:border-primary-400',
  idleClass: 'border-gray-200 bg-ui-surface hover:border-primary-500/50 dark:border-white/10 dark:hover:border-primary-400/50',
  accentClass: 'text-primary-500 dark:text-primary-400',
  mutedClass: 'text-gray-400 dark:text-gray-500',
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** This option was chosen. */
  'update:modelValue': [value: string | number]
}>()

/** Generated id used when the caller passes none. */
const generatedId = useId()

/** The id bound to the native radio and the label. */
const inputId = computed(() => props.id ?? generatedId)

/** Whether this option is the selected one. */
const selected = computed(() => props.modelValue === props.value)
</script>
