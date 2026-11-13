<script setup lang="ts">
/**
 * The message field of a chat: a frame holding a textarea that grows from one line to six as the reader types,
 * with the send control inside it (the `action` slot).
 *
 * Enter asks to send (`submit`) and Shift+Enter makes a new line; an Enter that confirms an input-method
 * composition is left alone. The character count shows only near the limit. The frame shows the focus ring when
 * the textarea has focus. `testid` is the textarea's `data-testid`.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'

/** The share of `maxLength` from which the character count shows. */
const COUNT_SHOWN_FROM = 0.8

/** Props for UiChatInput. */
const props = withDefaults(defineProps<{
  /** The text (v-model). */
  modelValue: string
  /** Hint shown inside the empty field. */
  placeholder: string
  /** Accessible name of the field, since it has no visible label. */
  label: string
  /** The most characters the field takes; the count shows near it. */
  maxLength?: number
  /** Disables the field. */
  disabled?: boolean
  /** `data-testid` of the textarea. */
  testid?: string
}>(), {
  maxLength: undefined,
  disabled: false,
  testid: undefined,
})

/** Events of UiChatInput. */
const emit = defineEmits<{
  /** The reader changed the text. */
  'update:modelValue': [value: string]
  /** The reader pressed Enter without Shift. */
  submit: []
}>()

/** The textarea. */
const field = ref<HTMLTextAreaElement | null>(null)

/** The number of characters typed. */
const length = computed<number>(() => props.modelValue.length)

/** Whether the count shows: only once the text is near the limit. */
const showCount = computed<boolean>(() => props.maxLength !== undefined && length.value >= props.maxLength * COUNT_SHOWN_FROM)

/** Class list of the frame. */
const frameClasses = computed<string[]>(() => [
  'flex w-full items-end gap-2 rounded-control border border-border px-3.5 py-1.5 transition-colors duration-200',
  'focus-within:border-text-accent focus-within:ring-2 focus-within:ring-text-accent/40',
  props.disabled ? 'cursor-not-allowed bg-surface-hover' : 'bg-surface-field',
])

/**
 * Fits the textarea to its text; the `max-h-36` class stops it at six lines, after which it scrolls.
 *
 * A field that is not shown (a closed tab, `display: none`) measures 0; it keeps its automatic height rather than
 * collapsing to nothing, and `observer` fits it once it is shown.
 *
 * @returns Nothing.
 */
const fit = (): void => {
  const element = field.value
  if (element) {
    element.style.height = 'auto'
    if (element.scrollHeight > 0) {
      element.style.height = `${element.scrollHeight}px`
    }
  }
}

/**
 * Forwards the typed text.
 *
 * @param event - The native input event.
 * @returns Nothing.
 */
const onInput = (event: Event): void => {
  emit('update:modelValue', (event.target as HTMLTextAreaElement).value)
}

/**
 * Turns Enter into a send, unless it confirms an input-method composition.
 *
 * @param event - The native key event.
 * @returns Nothing.
 */
const onEnter = (event: KeyboardEvent): void => {
  if (event.isComposing) {
    return
  }
  event.preventDefault()
  emit('submit')
}

/**
 * Puts the keyboard focus in the field.
 *
 * @returns Nothing.
 */
const focus = (): void => {
  field.value?.focus()
}

/** The frame a fit is waiting for; cancelled when the field goes away. */
let frame = 0

/**
 * Fits the field in the next frame. The size observer only schedules it: writing the height inside the observer's own
 * callback changes the layout it is still reporting, which the browser answers with a "ResizeObserver loop" warning.
 *
 * @returns Nothing.
 */
const fitNextFrame = (): void => {
  cancelAnimationFrame(frame)
  frame = requestAnimationFrame(fit)
}

/** Fits the field again whenever it changes size, which is also when it becomes visible. */
let observer: ResizeObserver | null = null

onMounted(() => {
  fit()
  if (field.value) {
    observer = new ResizeObserver(fitNextFrame)
    observer.observe(field.value)
  }
})
onBeforeUnmount(() => {
  cancelAnimationFrame(frame)
  observer?.disconnect()
  observer = null
})
// After the render, so the height is measured with the new text in place; a send empties the field and shrinks it again.
watch(() => props.modelValue, () => nextTick(fit))

/** The handle the parent reaches the field through. */
defineExpose({ focus })
</script>

<template>
  <div :class="frameClasses">
    <textarea
      ref="field"
      :value="modelValue"
      :placeholder="placeholder"
      :aria-label="label"
      :disabled="disabled"
      :maxlength="maxLength"
      :data-testid="testid"
      rows="1"
      class="max-h-36 min-w-0 flex-1 resize-none overflow-y-auto bg-transparent py-1.5 text-sm leading-6 text-text-primary outline-none placeholder:text-text-muted disabled:cursor-not-allowed disabled:text-text-muted"
      @input="onInput"
      @keydown.enter.exact="onEnter"
    />
    <span v-if="showCount" class="shrink-0 pb-2 text-xs text-text-muted" data-testid="ui-chat-input-count">{{ length }}/{{ maxLength }}</span>
    <div class="shrink-0 pb-0.5">
      <slot name="action" />
    </div>
  </div>
</template>
