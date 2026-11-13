<template>
  <div
    role="radiogroup"
    :aria-label="label"
    :data-testid="testid"
    :class="TRACK_CLASSES"
  >
    <button
      v-for="(option, index) in options"
      :key="String(option.value)"
      :ref="(el) => setSegmentRef(el, index)"
      type="button"
      role="radio"
      :aria-checked="option.value === modelValue"
      :tabindex="index === focusIndex ? 0 : -1"
      :data-testid="option.testid"
      :class="segmentClasses(option.value === modelValue)"
      @click="select(index)"
      @keydown="onKeydown($event, index)"
    >
      {{ option.label }}
    </button>
  </div>
</template>

<script setup lang="ts" generic="T extends SegmentedValue">
/**
 * UiSegmentedControl: a pill-shaped single choice between a few options (billing period,
 * cycle, view mode).
 *
 * A real radio group: `role="radiogroup"` with `role="radio"` segments, one tab stop (the chosen
 * segment) and arrow / Home / End keys that move the choice. The track and the segments share
 * the pill radius, every segment is the same width, the chosen one carries the brand gradient
 * with the brand's contrast text (`text-on-brand`: white on the saturated gradient in both colour modes), and the focus ring is drawn inside the pill. The control
 * owns its whole look, so a screen never passes override classes to a button.
 */
import type { ComponentPublicInstance } from 'vue'
import { computed, ref } from 'vue'
import type { SegmentedOption } from '~/types/segmentedoption'
import type { SegmentedValue } from '~/types/segmentedvalue'
import { UiSize } from '~/utils/ui/enums'

/** Props of UiSegmentedControl. */
interface Props {
  /** The segments, in display order. */
  options: SegmentedOption<T>[]
  /** The chosen value (v-model). */
  modelValue: T
  /** Accessible name of the group (what is being chosen). */
  label: string
  /** Size step of the segments. */
  size?: `${UiSize}`
  /** `data-testid` of the track. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  size: UiSize.Md,
  testid: undefined,
})

/** Events the control emits. */
const emit = defineEmits<{
  /** The reader chose another segment. */
  'update:modelValue': [value: T]
}>()

/** Pill track: equal-width columns that scroll sideways instead of overflowing the page when there are many, a hairline frame and a translucent fill that suits any surface. */
const TRACK_CLASSES = 'ui-segmented inline-grid max-w-full grid-flow-col auto-cols-fr gap-1 overflow-x-auto rounded-full border border-gray-200 bg-black/[0.04] p-1 dark:border-white/10 dark:bg-white/5'

/** Structure shared by every segment; the focus ring is inset so it follows the pill and never halos. */
const SEGMENT_BASE = 'ui-segment inline-flex items-center justify-center whitespace-nowrap rounded-full text-center font-semibold leading-none transition-all duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-offset-0'

/** Padding and type size per size step. */
const SEGMENT_SIZE_CLASSES: Record<UiSize, string> = {
  [UiSize.Sm]: 'min-w-[88px] px-3.5 py-2 text-xs',
  [UiSize.Md]: 'min-w-[120px] px-[18px] py-2.5 text-sm',
  [UiSize.Lg]: 'min-w-[140px] px-6 py-3 text-base',
}

/** The chosen segment: brand gradient with its contrast text; the ring takes the same colour so it reads on the gradient. */
const SEGMENT_ACTIVE = 'ui-segment-active bg-brand text-on-brand shadow-md focus-visible:ring-on-brand/80'

/** An available segment: readable token text that brightens on hover; its ring uses the brand. */
const SEGMENT_IDLE = 'text-gray-600 hover:text-gray-900 dark:text-gray-300 dark:hover:text-white focus-visible:ring-primary-500'

/** Rendered segment elements, by index, for moving focus with the keyboard. */
const segmentRefs = ref<HTMLButtonElement[]>([])

/**
 * Stores a segment element so the keyboard can focus it.
 *
 * @param el - The rendered element, or null when it unmounts.
 * @param index - Position of the segment.
 */
const setSegmentRef = (el: Element | ComponentPublicInstance | null, index: number): void => {
  if (el instanceof HTMLButtonElement) {
    segmentRefs.value[index] = el
  }
}

/** Index of the chosen segment. */
const selectedIndex = computed(() => props.options.findIndex(option => option.value === props.modelValue))

/** The single tab stop: the chosen segment, or the first one when nothing matches. */
const focusIndex = computed(() => (selectedIndex.value >= 0 ? selectedIndex.value : 0))

/**
 * Class list of one segment.
 *
 * @param active - Whether the segment is the chosen one.
 * @returns The classes.
 */
const segmentClasses = (active: boolean): string =>
  [SEGMENT_BASE, SEGMENT_SIZE_CLASSES[props.size as UiSize], active ? SEGMENT_ACTIVE : SEGMENT_IDLE].join(' ')

/**
 * Chooses a segment; choosing the current one emits nothing.
 *
 * @param index - Position of the segment.
 */
const select = (index: number): void => {
  const option = props.options[index]
  if (option && option.value !== props.modelValue) {
    emit('update:modelValue', option.value)
  }
}

/**
 * Moves the choice with the arrow, Home and End keys, wrapping at both ends, and follows it
 * with focus as a radio group does.
 *
 * @param event - The key event.
 * @param index - Position of the segment that has focus.
 */
const onKeydown = (event: KeyboardEvent, index: number): void => {
  const count = props.options.length
  if (count === 0) {
    return
  }
  let target: number
  if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {
    target = (index + 1) % count
  } else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {
    target = (index - 1 + count) % count
  } else if (event.key === 'Home') {
    target = 0
  } else if (event.key === 'End') {
    target = count - 1
  } else {
    return
  }
  event.preventDefault()
  select(target)
  segmentRefs.value[target]?.focus()
}
</script>
