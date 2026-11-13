<script setup lang="ts">
/**
 * The round mark that signs the replies of the other party in a chat.
 *
 * Decorative: the reply's own name line, when there is one, names the sender, so the mark is hidden from
 * assistive technology; `label` only gives sighted readers a tooltip.
 */
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import { UiSize } from '@/utils/ui/size'

/** Props for UiChatAvatar. */
withDefaults(defineProps<{
  /** The sender's name, shown as the tooltip. */
  label?: string
}>(), {
  label: undefined,
})
</script>

<template>
  <span
    class="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-tint-blue text-text-accent"
    :title="label"
    aria-hidden="true"
    data-testid="ui-chat-avatar"
  >
    <UiIcon :markup="Icon.Sparkles" :size="UiSize.Sm" />
  </span>
</template>
