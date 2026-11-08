<script setup lang="ts">
/**
 * A block that opens and closes by its height.
 *
 * Wrap the block in it and drive it with `open` instead of putting `v-if` on the block, so the leave can play:
 * the rows of a one-column grid animate from `0fr` to `1fr`, so the height is never measured. While it is closed
 * the content is not rendered, so nothing inside it takes focus or is read by a screen reader. While it closes
 * the content is inert, so it takes no focus during the leave, and when the focus was inside, it returns to the
 * control that was focused when the block opened (the toggle). Nothing moves under reduced motion (the shared
 * transition set removes it).
 */
import { watch } from 'vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'

/** Props for UiCollapse. */
const props = defineProps<{
  /** Whether the block is shown. */
  open: boolean
}>()

/** The control that was focused when the block opened: the toggle focus goes back to on close. */
let opener: HTMLElement | null = null

// Runs before the render, while the focus is still on the toggle that was pressed.
watch(
  () => props.open,
  (isOpen) => {
    // The close keeps the opener: the leave hook, which runs after this, still needs it.
    if (isOpen) {
      opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
    }
  },
)

/**
 * Starts the leave: gives the focus back to the toggle when it was inside the closing block, then makes the
 * block inert so nothing in it can be focused until it is gone.
 *
 * @param el - The closing block.
 */
const onBeforeLeave = (el: Element): void => {
  if (el.contains(document.activeElement)) {
    opener?.focus()
  }
  el.setAttribute('inert', '')
}
</script>

<template>
  <UiTransition :kind="UiTransitionKind.Collapse" @before-leave="onBeforeLeave">
    <div v-if="open" class="grid">
      <div class="min-h-0 [.ui-collapsing>&]:overflow-hidden">
        <slot />
      </div>
    </div>
  </UiTransition>
</template>
