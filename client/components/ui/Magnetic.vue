<template>
  <div
    ref="container"
    class="magnetic-wrapper"
    @mousemove="handleMouseMove"
    @mouseleave="handleMouseLeave"
  >
    <div
      ref="target"
      class="magnetic-target"
      :style="targetStyle"
    >
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Magnetic Wrapper Component
 * Makes any child element "magnetic" by attracting it to the mouse cursor.
 */

const props = withDefaults(defineProps<{
  strength?: number    // How much the element follows the mouse (0 to 1)
  range?: number       // The distance at which the effect starts (px)
  duration?: number    // Transition duration (ms)
}>(), {
  strength: 0.4,
  range: 80,
  duration: 600
})

const container = ref<HTMLElement | null>(null)
const target = ref<HTMLElement | null>(null)

const x = ref(0)
const y = ref(0)
const isHovered = ref(false)

const targetStyle = computed(() => ({
  transform: `translate3d(${x.value}px, ${y.value}px, 0)`,
  transition: isHovered.value ? 'none' : `transform ${props.duration}ms cubic-bezier(0.23, 1, 0.32, 1)`
}))

function handleMouseMove(e: MouseEvent) {
  if (!container.value) return

  const rect = container.value.getBoundingClientRect()
  const centerX = rect.left + rect.width / 2
  const centerY = rect.top + rect.height / 2
  
  const distanceX = e.clientX - centerX
  const distanceY = e.clientY - centerY

  // Calculate distance
  const distance = Math.sqrt(distanceX * distanceX + distanceY * distanceY)

  if (distance < props.range) {
    isHovered.value = true
    // Magnetic pull
    x.value = distanceX * props.strength
    y.value = distanceY * props.strength
  } else {
    handleMouseLeave()
  }
}

function handleMouseLeave() {
  isHovered.value = false
  x.value = 0
  y.value = 0
}
</script>

<style scoped>
.magnetic-wrapper {
  display: inline-block;
  cursor: pointer;
  position: relative;
  z-index: 10;
}

.magnetic-target {
  display: block;
  will-change: transform;
}
</style>
