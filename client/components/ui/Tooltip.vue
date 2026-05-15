<template>
  <div ref="wrapperRef" class="relative inline-block">
    <!-- Trigger -->
    <div @click="toggle">
      <slot />
    </div>

    <!-- Popover -->
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="opacity-0 scale-95 translate-y-1"
      enter-to-class="opacity-100 scale-100 translate-y-0"
      leave-active-class="transition duration-100 ease-in"
      leave-from-class="opacity-100 scale-100 translate-y-0"
      leave-to-class="opacity-0 scale-95 translate-y-1"
    >
      <div
        v-if="open"
        ref="tooltipRef"
        class="absolute z-50 top-full mt-1.5 rounded-xl border border-white/10 bg-gray-900 shadow-xl text-xs text-gray-300 leading-relaxed p-4 w-72 max-w-[calc(100vw-2rem)]"
        :style="tooltipStyle"
      >
        <slot name="content" />
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { onClickOutside } from '@vueuse/core'

const open        = ref(false)
const wrapperRef  = ref<HTMLElement | null>(null)
const tooltipRef  = ref<HTMLElement | null>(null)
const tooltipStyle = ref<Record<string, string>>({})

function computePosition() {
  if (!wrapperRef.value || !tooltipRef.value) return
  const rect    = wrapperRef.value.getBoundingClientRect()
  const tipW    = tooltipRef.value.offsetWidth
  const vw      = window.innerWidth
  const padding = 8 // min gap from screen edge

  // Try aligning to the right edge of the trigger first
  let right = vw - rect.right
  if (right + tipW > vw - padding) right = padding // clamp left overflow
  if (right < padding) right = padding             // clamp right overflow

  // Convert right → left offset relative to the wrapper
  const leftOffset = rect.right - rect.left - tipW + (vw - rect.right - right)
  tooltipStyle.value = { left: `${Math.max(padding - rect.left, leftOffset)}px` }
}

function toggle() {
  open.value = !open.value
  if (open.value) nextTick(computePosition)
}

onClickOutside(wrapperRef, () => { open.value = false })
</script>
