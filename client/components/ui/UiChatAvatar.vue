<template>
  <span
    class="grid size-7 shrink-0 place-items-center rounded-full bg-primary-500/15 text-primary-700 dark:text-primary-300"
    :title="label"
    aria-hidden="true"
    data-testid="ui-chat-avatar"
  >
    <Sparkles :size="14" :stroke-width="2" />
  </span>
</template>

<script setup lang="ts">
/**
 * UiChatAvatar: the round mark that signs the replies of the other party in a chat.
 *
 * Decorative: the reply's own name line, when there is one, names the sender, so the mark is hidden from assistive
 * technology; `label` only gives sighted readers a tooltip.
 */
import { Sparkles } from 'lucide-vue-next'

/** Props of UiChatAvatar. */
interface Props {
  /** The sender's name, shown as the tooltip. */
  label?: string
}

/** The props, read in the template. */
withDefaults(defineProps<Props>(), {
  label: undefined,
})
</script>
