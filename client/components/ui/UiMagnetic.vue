<template>
  <div
    ref="container"
    class="inline-block relative z-10 cursor-pointer"
    @mousemove="handleMouseMove"
    @mouseleave="handleMouseLeave"
  >
    <div
      ref="target"
      data-testid="ui-magnetic-target"
      :class="targetClasses"
    >
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiMagnetic: makes its slot content drift toward the mouse while the cursor is near.
 *
 * The moving offset and the settle duration are CSS custom properties (`--mag-x`, `--mag-y`,
 * `--mag-duration`) written on the inner element from script; the transform itself is a Tailwind
 * class reading them. That keeps the component free of an inline `style` attribute while the
 * value still changes every mouse move.
 */
import { computed, onMounted, ref, watch } from 'vue'

/** Props of UiMagnetic. */
interface Props {
  /** How much the content follows the mouse, 0 to 1. */
  strength?: number
  /** Distance in px at which the pull starts. */
  range?: number
  /** Settle transition in ms once the cursor leaves. */
  duration?: number
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  strength: 0.4,
  range: 80,
  duration: 600,
})

/** The outer element, measured for the cursor distance. */
const container = ref<HTMLElement | null>(null)

/** The moving element that receives the custom properties. */
const target = ref<HTMLElement | null>(null)

/** Horizontal offset in px. */
const x = ref(0)

/** Vertical offset in px. */
const y = ref(0)

/** Whether the cursor is inside the pull range (no transition while it is). */
const isHovered = ref(false)

/** Class list of the moving element. */
const targetClasses = computed(() => [
  'block will-change-transform translate-x-[var(--mag-x,0px)] translate-y-[var(--mag-y,0px)]',
  isHovered.value ? 'transition-none' : 'transition-transform duration-[var(--mag-duration,600ms)] ease-[cubic-bezier(0.23,1,0.32,1)]',
].join(' '))

/** Writes the current offset and duration onto the moving element as custom properties. */
const syncVariables = (): void => {
  const element = target.value
  if (!element) {
    return
  }
  element.style.setProperty('--mag-x', `${x.value}px`)
  element.style.setProperty('--mag-y', `${y.value}px`)
  element.style.setProperty('--mag-duration', `${props.duration}ms`)
}

/** Releases the pull and returns the content to rest. */
const handleMouseLeave = (): void => {
  isHovered.value = false
  x.value = 0
  y.value = 0
}

/**
 * Pulls the content toward the cursor while it is within range.
 *
 * @param event The mouse move over the wrapper.
 */
const handleMouseMove = (event: MouseEvent): void => {
  if (!container.value) {
    return
  }

  const rect = container.value.getBoundingClientRect()
  const distanceX = event.clientX - (rect.left + rect.width / 2)
  const distanceY = event.clientY - (rect.top + rect.height / 2)

  if (Math.sqrt(distanceX * distanceX + distanceY * distanceY) < props.range) {
    isHovered.value = true
    x.value = distanceX * props.strength
    y.value = distanceY * props.strength
  } else {
    handleMouseLeave()
  }
}

onMounted(syncVariables)
watch([x, y, () => props.duration], syncVariables)
</script>
