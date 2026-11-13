<script setup lang="ts">
/**
 * One message of a ticket thread: a chat bubble that carries the sender's name and the time. A staff message
 * sits on the right in the tinted bubble with a "Staff" badge; the client's message sits on the left in the
 * frosted one. The message text is the default slot, so a caller passes text, never markup.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiChatBubble, { UiChatBubbleSide } from '@/components/ui/UiChatBubble.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatDateTimeCompact } from '@/lib/format'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for SupportTicketMessage. */
const props = defineProps<{
  /** Name of the sender, already resolved for display. */
  author: string
  /** ISO 8601 timestamp the message was sent at. */
  createdAt: string
  /** Whether a staff member wrote the message. */
  staff: boolean
}>()

/** Side of the thread the message belongs to: staff are the reader's own side. */
const side = computed<UiChatBubbleSide>(() => (props.staff ? UiChatBubbleSide.Own : UiChatBubbleSide.Other))

/** The send time, formatted for display. */
const time = computed<string>(() => formatDateTimeCompact(props.createdAt))
</script>

<template>
  <UiChatBubble :side="side" :time="time">
    <template #sender>
      <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Sm" class="font-medium" data-testid="ticket-message-author">
        {{ author }}
      </UiText>
      <UiBadge v-if="staff" :tone="UiTone.Primary" small>
        {{ t('support.supportTicketDetail.staff') }}
      </UiBadge>
    </template>
    <slot />
  </UiChatBubble>
</template>
