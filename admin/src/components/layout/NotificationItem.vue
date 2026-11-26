<script setup lang="ts">
/**
 * One row of the notification drawer.
 *
 * A type icon in a circle tinted with the event's tone, the localised title, a second line with the client
 * and the amount, the time in the active locale, and (while unread) a dot, a subtle background and a button
 * that marks it read. The whole row is a link to the record the event is about; opening it emits `open` so the
 * drawer can mark it read and close.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiRowLink from '@/components/ui/UiRowLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useActivityEventText } from '@/composables/useActivityEventText'
import { activityEventLink } from '@/lib/activityEventLink'
import { ACTIVITY_EVENT_LOOK } from '@/lib/activityEventLook'
import { Icon } from '@/lib/icons'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiRowLinkLook } from '@/utils/ui/rowLink'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { toneBadgeClasses, UiTone } from '@/utils/ui/tone'
import type { ActivityEvent } from '@/types/activityevent'

/** Props for NotificationItem. */
const props = defineProps<{
  /** The feed event the row shows. */
  event: ActivityEvent
  /** Whether the operator has not read it yet. */
  unread: boolean
  /** Position of the row in the list, for the `data-testid`s. */
  index: number
}>()

const emit = defineEmits<{
  /** Emitted when the row's link is followed. */
  open: [event: ActivityEvent]
  /** Emitted when the operator marks the row read without opening it. */
  read: [event: ActivityEvent]
}>()

const { t } = useI18n()

/** Title, detail and time of an event. */
const text = useActivityEventText()

/** The icon and tone of the event's type. */
const look = computed(() => ACTIVITY_EVENT_LOOK[props.event.type])

/** Classes of the tinted circle that frames the icon. */
const circleClasses = computed<string>(() => `h-9 w-9 shrink-0 rounded-full ${toneBadgeClasses[look.value.tone]}`)

/** The second line: the client and the amount. */
const detail = computed<string>(() => text.detail(props.event))
</script>

<template>
  <UiStack
    :direction="UiDirection.Row"
    :align="UiAlignItems.Start"
    :gap="UiGap.None"
    class="border-b border-border last:border-0 transition-colors"
    :class="unread ? 'bg-primary-500/5' : ''"
    :data-testid="`notification-${index}`"
    :data-unread="unread ? 'true' : 'false'"
  >
    <UiRowLink :to="activityEventLink(event)" :look="UiRowLinkLook.Block" class="!flex min-w-0 flex-1 items-start gap-3 px-4 py-3.5 hover:bg-surface-hover" :data-testid="`notification-${index}-link`" @click="emit('open', event)">
      <UiStack :align="UiAlignItems.Center" :justify="UiJustify.Center" :class="circleClasses" :data-testid="`notification-${index}-icon`" :data-tone="look.tone">
        <UiIcon :markup="look.icon" :size="UiSize.Sm" />
      </UiStack>
      <UiStack :gap="UiGap.Xs" class="min-w-0 flex-1">
        <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md" :class="unread ? 'font-semibold' : ''">{{ text.title(event) }}</UiText>
        <UiText v-if="detail !== ''" :tone="UiTextTone.Secondary" :size="UiTextSize.Sm" :data-testid="`notification-${index}-detail`">{{ detail }}</UiText>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" :data-testid="`notification-${index}-time`">{{ text.time(event) }}</UiText>
      </UiStack>
      <UiText v-if="unread" class="sr-only">{{ t('notifications.unread') }}</UiText>
      <UiBadge v-if="unread" :tone="UiTone.Primary" marker class="mt-1.5" :data-testid="`notification-${index}-dot`" />
    </UiRowLink>
    <UiIconButton v-if="unread" :label="t('notifications.markRead')" :size="UiSize.Sm" class="mt-2.5 mr-2" :data-testid="`notification-${index}-mark-read`" @click="emit('read', event)">
      <UiIcon :markup="Icon.Check" :size="UiSize.Sm" />
    </UiIconButton>
  </UiStack>
</template>
