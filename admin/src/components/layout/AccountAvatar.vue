<script setup lang="ts">
/**
 * The round initials avatar of the signed-in account.
 *
 * Drawn by the account card beside the name, and alone as the menu trigger of the folded icon rail,
 * where it is sized (32px) to sit inside the rail's 40px tile with its border and padding.
 */
import UiText from '@/components/ui/UiText.vue'
import { UiTextTag, UiTextTone } from '@/utils/ui/text'

/** Props of the account avatar. */
defineProps<{
  /** The letters drawn in the circle. */
  initials: string
  /** Draws the larger avatar that fills the folded rail's tile. */
  rail?: boolean
}>()
</script>

<template>
  <UiText
    :as="UiTextTag.Span"
    :tone="UiTextTone.Plain"
    class="grid shrink-0 place-items-center rounded-full bg-avatar-bg text-xs font-bold text-avatar-fg"
    :class="rail ? 'size-8' : 'size-7'"
    data-testid="account-avatar"
  >
    {{ initials }}
  </UiText>
</template>
