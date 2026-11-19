<script setup lang="ts">
/**
 * Date and time picker for an optional moment, with the popup following the panel's light/dark scheme.
 *
 * Wraps the date-picker library so a screen does not configure it (or restyle its input) itself.
 * The model is a `Date` or `null`; the clear button sets `null`. `minDate` stops the pick earlier
 * than another field, e.g. an end moment before its start. The library's colours, radius and font come
 * from the panel tokens through its CSS variables (`dateLibraryThemeClasses` on a wrapper), so the popup
 * is opaque and themed in both schemes. On a phone (below `sm`) the library's own popup gives way to the kit's bottom
 * sheet ({@link UiFloatingSurface}): the field is a button showing the chosen moment, and the library draws its calendar
 * and clock inline inside the sheet, with Clear and Apply beneath.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { VueDatePicker } from '@vuepic/vue-datepicker'
import UiButton from '@/components/ui/UiButton.vue'
import UiFloatingSurface from '@/components/ui/UiFloatingSurface.vue'
import { useDrawerPresentation } from '@/composables/useDrawerPresentation'
import { useDropdown } from '@/composables/useDropdown'
import { useThemeStore } from '@/stores/theme'
import { UiActionKind } from '@/utils/ui/actions'
import { dateLibraryThemeClasses } from '@/utils/ui/datePicker'
import { UiDrawerPresentation } from '@/utils/ui/drawer'
import { fieldBaseClasses, fieldWidthClasses } from '@/utils/ui/input'
import { fieldSizeClasses } from '@/utils/ui/size'
import '@vuepic/vue-datepicker/dist/main.css'

const { t, locale } = useI18n()

/** Theme store; the library has its own dark/light switch, so it is fed the panel's scheme. */
const theme = useThemeStore()

/** Props for UiDateTimePicker. */
const props = withDefaults(defineProps<{
  /** Chosen moment (two-way bound with `v-model`); `null` for none. */
  modelValue: Date | null
  /** Hint shown while nothing is chosen (already translated). */
  placeholder?: string
  /** Earliest moment that can be chosen. */
  minDate?: Date
}>(), {
  placeholder: undefined,
  minDate: undefined,
})

const emit = defineEmits<{
  /** Emitted with the new moment, or `null` when it was cleared. */
  'update:modelValue': [value: Date | null]
}>()

/** Classes for the library's input, so it matches the kit's fields (the library's own CSS is overridden). */
const INPUT_CLASSES = 'bg-surface-field! rounded-control! h-10.5! px-3.5! text-sm! placeholder:text-text-muted! focus-visible:ring-2! focus-visible:ring-text-accent/40!'

/** Free room below the field, in pixels, under which a popover would open above it (the sheet ignores it). */
const SHEET_MIN_ROOM_PX = 420

/** Gap between the field and a popover, in pixels (the sheet ignores it). */
const SHEET_GAP_PX = 8

/** The shape the screen asks for: on a phone the picker is a bottom sheet. */
const presentation = useDrawerPresentation()

/** Whether the picker is a bottom sheet now. */
const isSheet = computed<boolean>(() => presentation.value === UiDrawerPresentation.Sheet)

/** Open flag and element refs of the sheet's floating surface. */
const { open, root, trigger, panel, hide, toggle } = useDropdown()

/** The chosen moment as the field shows it on a phone, in the active language; empty while nothing is chosen. */
const shown = computed<string>(() =>
  props.modelValue === null ? '' : props.modelValue.toLocaleString(locale.value, { dateStyle: 'medium', timeStyle: 'short' }),
)

/**
 * Re-emits the library's value as a `Date` or `null` (its model type is wider than ours).
 *
 * @param value - The value the library reported.
 */
const onUpdate = (value: unknown): void => {
  emit('update:modelValue', value instanceof Date ? value : null)
}

/** Clears the moment and closes the sheet. */
const clear = (): void => {
  emit('update:modelValue', null)
  hide(true)
}
</script>

<template>
  <div v-if="!isSheet" :class="dateLibraryThemeClasses">
    <VueDatePicker
      :model-value="modelValue"
      :enable-time-picker="true"
      :clearable="true"
      :dark="!theme.isLight"
      :auto-apply="true"
      :min-date="minDate"
      :placeholder="placeholder"
      :input-class-name="INPUT_CLASSES"
      @update:model-value="onUpdate"
    />
  </div>
  <div v-else ref="root" :class="dateLibraryThemeClasses">
    <button
      ref="trigger"
      type="button"
      aria-haspopup="dialog"
      :aria-expanded="open"
      data-testid="date-time-trigger"
      :class="[fieldBaseClasses, fieldSizeClasses.md, fieldWidthClasses, 'text-left', shown === '' ? 'text-text-muted' : '']"
      @click="toggle"
    >
      {{ shown === '' ? placeholder : shown }}
    </button>

    <UiFloatingSurface
      :open="open"
      :anchor="trigger"
      :title="placeholder ?? t('ui.uiDateTimePicker.title')"
      :min-room="SHEET_MIN_ROOM_PX"
      :gap="SHEET_GAP_PX"
      @close="hide(true)"
      @panel="panel = $event"
    >
      <VueDatePicker
        inline
        :model-value="modelValue"
        :enable-time-picker="true"
        :dark="!theme.isLight"
        :auto-apply="true"
        :min-date="minDate"
        @update:model-value="onUpdate"
      />
      <div class="flex justify-between gap-2 pt-3">
        <UiButton :action="UiActionKind.Clear" variant="ghost" type="button" data-testid="date-time-clear" @click="clear">
          {{ t('shared.clear') }}
        </UiButton>
        <UiButton :action="UiActionKind.Apply" variant="primary" type="button" data-testid="date-time-apply" @click="hide(true)">
          {{ t('shared.apply') }}
        </UiButton>
      </div>
    </UiFloatingSurface>
  </div>
</template>
