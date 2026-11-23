<script setup lang="ts">
/**
 * Full-page admin notification feed: the destination of the top bar bell's "View all" link.
 *
 * Shows every event type the feed carries (client registrations, overdue invoices, domain
 * expiries), not just invoices. Reads `stores/activity`; each row links to the record it is about.
 * A refused load shows the API's sentence with a retry.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowLink from '@/components/ui/UiRowLink.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { activityEventLink } from '@/lib/activityEventLink'
import { formatRelativeFromNow } from '@/lib/format'
import { placeholderRows } from '@/lib/placeholderRows'
import { ACTIVITY_EVENT_LOOK } from '@/lib/activityEventLook'
import { useActivityStore } from '@/stores/activity'
import { UiWidth, UiGap } from '@/utils/ui/layout'
import { UiRowLinkLook } from '@/utils/ui/rowLink'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

const { t, locale } = useI18n()

/** Activity store: the feed and its load state. */
const activity = useActivityStore()

/** How many events the full page asks for (the bell previews fewer). */
const FEED_LIMIT = 50

/** Placeholder rows drawn while the feed loads. */
const SKELETON_ROWS = 5

/** The rows of the list: the events, or null placeholders while the feed loads. */
const rows = computed(() => placeholderRows(activity.events, activity.initialLoading, SKELETON_ROWS))

onMounted(() => activity.fetchActivity(FEED_LIMIT))
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('common.notifications')"
      :subtitle="t('common.viewAllNotifications')"
      data-testid="notifications-header"
    />

    <UiErrorState
      v-if="activity.error"
      :message="activity.error"
      :loading="activity.loading"
      testid="notifications-error"
      @retry="activity.fetchActivity(FEED_LIMIT)"
    />

    <UiCard v-else-if="!activity.initialLoading && activity.events.length === 0">
      <UiEmptyState :title="t('common.noNotifications')" data-testid="notifications-empty" />
    </UiCard>

    <!-- The feed; while it loads the same rows are drawn with bars where the event goes -->
    <UiCard v-else :aria-busy="activity.initialLoading ? 'true' : undefined" data-testid="notifications-list">
      <UiRowLink
        v-for="(event, index) in rows"
        :key="event ? `${event.type}-${event.entityId}-${event.occurredAt}` : `placeholder-${index}`"
        :to="event ? activityEventLink(event) : undefined"
        :look="UiRowLinkLook.Divided"
        :data-testid="`notification-${index}`"
      >
        <UiSkeletonBar v-if="event === null" class="h-4 w-4 rounded-full" aria-hidden="true" />
        <UiIcon v-else :markup="ACTIVITY_EVENT_LOOK[event.type].icon" :size="UiSize.Sm" :class="ACTIVITY_EVENT_LOOK[event.type].color" />
        <UiStack v-if="event" :gap="UiGap.Xs">
          <UiText>{{ event.message }}</UiText>
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ formatRelativeFromNow(event.occurredAt, locale) }}</UiText>
        </UiStack>
        <UiStack v-else :gap="UiGap.Xs" class="flex-1" aria-hidden="true">
          <UiSkeletonBar :width="UiWidth.TwoThirds" class="h-3.5 rounded" />
          <UiSkeletonBar :width="UiWidth.Xs" class="h-3 rounded" />
        </UiStack>
      </UiRowLink>
    </UiCard>
  </UiStack>
</template>
