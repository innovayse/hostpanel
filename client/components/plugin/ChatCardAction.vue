<template>
  <template v-if="visible">
    <PluginActionBlock v-if="action.block" :block="action.block" />
    <UiButton v-else-if="action.resend" size="sm" :disabled="disabled" data-testid="plugin-chat-result-resend" @click="emit('resend')">{{ action.label }}</UiButton>
    <UiLink v-else-if="action.link?.external" :href="action.link.href" external testid="plugin-chat-result-link">{{ action.label }}</UiLink>
    <UiLink v-else-if="action.link" :to="action.link.href" testid="plugin-chat-result-link">{{ action.label }}</UiLink>
  </template>
</template>

<script setup lang="ts">
/**
 * PluginChatCardAction - one action of a chat's result card: a call drawn as an action block, a "try again" that asks
 * the chat to send its last message again, or a checked link. An action with a `visibleWhen` condition shows only while
 * the condition holds (`usePluginCondition`): it is hidden while the condition is read, when the read fails and when it
 * does not hold, and it is read again whenever one of the blocks the chat reads again is refreshed, so a card that
 * offered "Publish version 5" stops offering it once version 5 is live or a newer version has replaced it. An action
 * with no condition always shows and reads nothing. The card is old text in a log; this is what keeps its buttons honest.
 *
 * It reports whether it shows (`visibility`), so the row that holds the card's actions can hide itself when every action
 * is hidden. A hidden action stays mounted: it is what reads the condition again at the next refresh.
 */
import { usePluginCondition } from '~/composables/usePluginView'
import type { PluginChatResultAction } from '~/types/pluginchatresult'

/** Props of PluginChatCardAction. */
interface Props {
  /** The action, as the event reader parsed it. */
  action: PluginChatResultAction
  /** The ids of the blocks the chat reads again after a turn; a refresh of any of them reads the condition again. */
  refresh?: readonly string[]
  /** Whether a "try again" is disabled, as while a reply streams. */
  disabled?: boolean
}

/** The props. */
const props = withDefaults(defineProps<Props>(), { refresh: () => [], disabled: false })

/** The events of PluginChatCardAction. */
const emit = defineEmits<{
  /** The reader asked to send the conversation's last message again. */
  resend: []
  /** Whether the action shows, told when it mounts and whenever that changes. */
  visibility: [shown: boolean]
}>()

/** Whether the action shows: always without a condition, otherwise only while the condition holds. */
const visible = usePluginCondition(() => props.action.visibleWhen, () => props.refresh)

watch(visible, shown => emit('visibility', shown), { immediate: true })
</script>
