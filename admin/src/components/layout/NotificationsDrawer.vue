<script setup lang="ts">
/**
 * The notification feed of the top bar, in a side drawer (a bottom sheet on a phone).
 *
 * The header says how many events are unread and marks them all read; an All / Unread switch narrows the list;
 * the events are grouped by day (Upcoming, Today, Earlier) and drawn as `NotificationItem` rows; an empty feed
 * or an empty filter shows its own message; and "View all" is pinned in the footer. Opening a row marks it read
 * and closes the drawer. The feed itself and the read state live in `stores/activity`.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import NotificationItem from '@/components/layout/NotificationItem.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSegmented from '@/components/ui/UiSegmented.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useDrawerPresentation } from '@/composables/useDrawerPresentation'
import { ActivityFilter, groupActivityEvents } from '@/lib/activityFeed'
import { Icon } from '@/lib/icons'
import { AppRoute } from '@/lib/routes'
import { useActivityStore } from '@/stores/activity'
import type { ActivityEvent } from '@/types/activityevent'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Props for NotificationsDrawer. */
defineProps<{
  /** Whether the drawer is shown. */
  open: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the drawer should close: Escape, the backdrop, the close button, an opened row or "View all". */
  close: []
}>()

const { t } = useI18n()

/** Activity store: the feed and which events are read. */
const activity = useActivityStore()

/** The shape of the drawer: a bottom sheet on a phone, a side panel from `sm` up. */
const presentation = useDrawerPresentation()

/** How many placeholder rows are drawn while the feed loads. */
const PLACEHOLDER_ROWS = 3

/** Which events are listed. */
const filter = ref<ActivityFilter>(ActivityFilter.All)

/** The All / Unread switch. */
const filterItems = computed<UiTabItem[]>(() => [
  { value: ActivityFilter.All, label: t('notifications.filter.all'), testid: 'notifications-filter-all' },
  { value: ActivityFilter.Unread, label: t('notifications.filter.unread'), testid: 'notifications-filter-unread' },
])

/** The events the current filter lists. */
const visibleEvents = computed<ActivityEvent[]>(() => (filter.value === ActivityFilter.Unread ? activity.unreadEvents : activity.events))

/** The listed events under their day headings. */
const groups = computed(() => groupActivityEvents(visibleEvents.value))

/** Message of the empty state: nothing at all, or nothing unread. */
const emptyTitle = computed<string>(() => (filter.value === ActivityFilter.Unread && activity.events.length > 0 ? t('notifications.emptyUnread') : t('common.noNotifications')))

/**
 * Handles a row being opened: it is read from now on and the drawer gets out of the way.
 *
 * @param event - The event whose row was followed.
 */
const onOpen = (event: ActivityEvent): void => {
  void activity.markRead(event)
  emit('close')
}
</script>

<template>
  <UiDrawer :open="open" :title="t('common.notifications')" :presentation="presentation" flush @close="emit('close')">
    <UiStack :gap="UiGap.None" data-testid="notifications-drawer">
      <!-- Header: the unread count, mark all read and the filter -->
      <UiStack :gap="UiGap.Sm" class="border-b border-border px-4 py-3">
        <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between" :gap="UiGap.Sm">
          <UiBadge :tone="activity.unreadCount > 0 ? UiTone.Primary : UiTone.Neutral" data-testid="notifications-unread-count">
            {{ t('notifications.unreadCount', { count: activity.unreadCount }, activity.unreadCount) }}
          </UiBadge>
          <UiButton type="button" :variant="UiButtonVariant.Link" :icon="Icon.CheckCircle" :disabled="activity.unreadCount === 0" data-testid="notifications-mark-all-read" @click="activity.markAllRead()">
            {{ t('notifications.markAllRead') }}
          </UiButton>
        </UiStack>
        <UiSegmented v-model="filter" :items="filterItems" :aria-label="t('notifications.filter.label')" data-testid="notifications-filter" />
      </UiStack>

      <!-- Loading: the same rows drawn as bars -->
      <UiStack v-if="activity.initialLoading" :gap="UiGap.None" aria-busy="true" data-testid="notifications-loading">
        <UiStack v-for="index in PLACEHOLDER_ROWS" :key="index" :direction="UiDirection.Row" :gap="UiGap.Sm" class="border-b border-border px-4 py-3.5" aria-hidden="true">
          <UiSkeletonBar class="h-9 w-9 shrink-0 rounded-full" />
          <UiStack :gap="UiGap.Xs" class="flex-1">
            <UiSkeletonBar class="h-4 w-full rounded" />
            <UiSkeletonBar class="h-3 w-24 rounded" />
          </UiStack>
        </UiStack>
      </UiStack>

      <UiEmptyState v-else-if="visibleEvents.length === 0" :title="emptyTitle" :description="t('notifications.emptyHint')" data-testid="notifications-empty">
        <template #icon>
          <UiIcon :markup="Icon.CheckCircle" :size="UiSize.Md" />
        </template>
      </UiEmptyState>

      <!-- The feed, one block per day group -->
      <template v-else>
        <UiStack v-for="bucket in groups" :key="bucket.group" :gap="UiGap.None" :data-testid="`notifications-group-${bucket.group}`">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="bg-surface-hover px-4 py-1.5 font-semibold tracking-wide uppercase">{{ t(`notifications.group.${bucket.group}`) }}</UiText>
          <NotificationItem
            v-for="event in bucket.events"
            :key="`${event.type}-${event.entityId}-${event.occurredAt}`"
            :event="event"
            :unread="!event.isRead"
            :index="visibleEvents.indexOf(event)"
            @open="onOpen"
            @read="activity.markRead"
          />
        </UiStack>
      </template>
    </UiStack>

    <template #footer>
      <UiLink :to="AppRoute.Notifications" :icon="Icon.ArrowRight" :aria-label="t('common.viewAllNotifications')" data-testid="notifications-view-all" @click="emit('close')">
        {{ t('shared.viewAll') }}
      </UiLink>
    </template>
  </UiDrawer>
</template>
