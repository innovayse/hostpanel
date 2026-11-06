<script setup lang="ts">
/**
 * A block that opens and closes by its height.
 *
 * Wrap the block in it and drive it with `open` instead of putting `v-if` on the block, so the leave can play:
 * the rows of a one-column grid animate from `0fr` to `1fr`, so the height is never measured. While it is closed
 * the content is not rendered, so nothing inside it takes focus or is read by a screen reader. Nothing moves
 * under reduced motion (the shared transition set removes it).
 */
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'

/** Props for UiCollapse. */
defineProps<{
  /** Whether the block is shown. */
  open: boolean
}>()
</script>

<template>
  <UiTransition :kind="UiTransitionKind.Collapse">
    <div v-if="open" class="grid">
      <div class="min-h-0 [.ui-collapsing>&]:overflow-hidden">
        <slot />
      </div>
    </div>
  </UiTransition>
</template>
