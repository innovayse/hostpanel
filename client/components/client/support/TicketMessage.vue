<template>
  <UiCard padding="md" :class="staff ? 'border-primary-500/20 bg-primary-500/5' : ''" data-testid="ticket-message">
    <UiStack direction="row" align="center" justify="between" class="mb-4">
      <UiStack direction="row" align="center" gap="sm">
        <UiAvatar :first-name="author" size="sm" />
        <UiStack gap="none">
          <UiText as="span" size="sm" weight="medium">{{ author }}</UiText>
          <UiText as="span" size="xs" tone="subtle">{{ date }}</UiText>
        </UiStack>
      </UiStack>
      <UiBadge v-if="badge" :variant="staff ? 'primary' : 'info'" size="sm" rounded>{{ badge }}</UiBadge>
    </UiStack>
    <!-- Plain text with its line breaks kept; never rendered as markup. -->
    <UiText size="sm" tone="muted" class="leading-relaxed whitespace-pre-wrap">{{ message }}</UiText>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * TicketMessage. One message of a support ticket thread: who wrote it, when, a badge naming the
 * side (you / staff) and the text with its line breaks kept.
 */

/** Props of TicketMessage. */
interface Props {
  /** Display name of the author. */
  author: string
  /** When the message was posted, already formatted for display. */
  date: string
  /** The message body, shown as plain text. */
  message: string
  /** Short label naming the side the message came from. */
  badge?: string
  /** Whether a member of staff wrote it (tints the card). */
  staff?: boolean
}

withDefaults(defineProps<Props>(), {
  badge: undefined,
  staff: false,
})
</script>
