<template>
  <div :class="SCROLLER_CLASSES" :data-testid="testid">
    <div
      role="tablist"
      :aria-label="label"
      :class="LIST_CLASSES"
    >
      <button
        v-for="(tab, index) in tabs"
        :key="tab.value"
        :ref="(el) => setTabRef(el, index)"
        type="button"
        role="tab"
        :aria-selected="tab.value === modelValue"
        :tabindex="index === focusIndex ? 0 : -1"
        :data-testid="tab.testid"
        :class="tabClasses(tab.value === modelValue)"
        @click="select(index)"
        @keydown="onKeydown($event, index)"
      >
        {{ tab.label }}
        <span v-if="tab.count !== undefined" class="ui-chip-count ml-1.5 font-mono text-[0.85em] opacity-60">{{ tab.count }}</span>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts" generic="T extends string">
/**
 * UiChipTabs: a row of pill chips that filters one list (domain category, FAQ topic, month).
 *
 * A real tab list: `role="tablist"` with `role="tab"` chips, `aria-selected`, one tab stop (the
 * chosen chip) and arrow / Home / End keys that move the choice and focus. The chosen chip
 * carries the brand gradient with the brand's white text (`text-on-brand`, the same token as
 * `UiSegmentedControl`), the others are framed token-coloured pills with a hover state, and
 * the focus ring is drawn inside the pill. The row centres when it fits and scrolls
 * horizontally inside its own box when it does not, so a long list never widens the page.
 */
import type { ComponentPublicInstance } from 'vue'
import { computed, ref } from 'vue'
import type { ChipTab } from '~/types/chiptab'

/** Props of UiChipTabs. */
interface Props {
  /** The chips, in display order. */
  tabs: ChipTab<T>[]
  /** The chosen value (v-model). */
  modelValue: T
  /** Accessible name of the tab list (what is being filtered). */
  label: string
  /** `data-testid` of the scrolling wrapper. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  testid: undefined,
})

/** Events the component emits. */
const emit = defineEmits<{
  /** The reader chose another chip. */
  'update:modelValue': [value: T]
}>()

/** Scroll box: its own horizontal scroll, scrollbar hidden, a little room so a ring is never cut. */
const SCROLLER_CLASSES = 'no-scrollbar w-full max-w-full overflow-x-auto overscroll-x-contain py-1'

/** The row: as wide as the chips need, but never narrower than the box, so `justify-center` centres without clipping the first chip. */
const LIST_CLASSES = 'flex w-max min-w-full justify-center gap-2 px-1'

/** Structure shared by every chip (`ui-chip` is the template hook classic.css restyles); the focus ring is inset so it follows the pill and is never clipped by the scroll box. */
const TAB_BASE = 'ui-chip inline-flex shrink-0 items-center justify-center whitespace-nowrap rounded-full border px-3.5 py-2 text-[13px] font-semibold leading-none transition-colors duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-offset-0'

/** The chosen chip: brand gradient with its contrast text; the ring takes the same colour so it reads on the gradient. */
const TAB_ACTIVE = 'ui-chip-active border-transparent bg-brand text-on-brand shadow-md focus-visible:ring-on-brand/80'

/** An available chip: readable token text and a hairline frame that brighten on hover; its ring uses the brand. */
const TAB_IDLE = 'border-gray-200 text-gray-600 hover:bg-black/[0.04] hover:text-gray-900 focus-visible:ring-primary-500 dark:border-white/10 dark:text-gray-300 dark:hover:bg-white/5 dark:hover:text-white'

/** Rendered chip elements, by index, for moving focus with the keyboard. */
const tabRefs = ref<HTMLButtonElement[]>([])

/**
 * Stores a chip element so the keyboard can focus it.
 *
 * @param el - The rendered element, or null when it unmounts.
 * @param index - Position of the chip.
 */
const setTabRef = (el: Element | ComponentPublicInstance | null, index: number): void => {
  if (el instanceof HTMLButtonElement) {
    tabRefs.value[index] = el
  }
}

/** Index of the chosen chip. */
const selectedIndex = computed(() => props.tabs.findIndex(tab => tab.value === props.modelValue))

/** The single tab stop: the chosen chip, or the first one when nothing matches. */
const focusIndex = computed(() => (selectedIndex.value >= 0 ? selectedIndex.value : 0))

/**
 * Class list of one chip.
 *
 * @param active - Whether the chip is the chosen one.
 * @returns The classes.
 */
const tabClasses = (active: boolean): string => [TAB_BASE, active ? TAB_ACTIVE : TAB_IDLE].join(' ')

/**
 * Chooses a chip; choosing the current one emits nothing.
 *
 * @param index - Position of the chip.
 */
const select = (index: number): void => {
  const tab = props.tabs[index]
  if (tab && tab.value !== props.modelValue) {
    emit('update:modelValue', tab.value)
  }
}

/**
 * Moves the choice with the arrow, Home and End keys, wrapping at both ends, and follows it
 * with focus (automatic activation, as a tab list does).
 *
 * @param event - The key event.
 * @param index - Position of the chip that has focus.
 */
const onKeydown = (event: KeyboardEvent, index: number): void => {
  const count = props.tabs.length
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
  tabRefs.value[target]?.focus()
}
</script>
