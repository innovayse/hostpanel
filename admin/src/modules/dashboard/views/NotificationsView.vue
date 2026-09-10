<script setup lang="ts">
/**
 * Full-page admin notification feed — the destination for the Topbar bell's
 * "View all notifications" link. Shows every event type the feed carries
 * (client registrations, overdue invoices, domain expiries), not just invoices.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatDistanceToNowStrict, type Locale } from 'date-fns'
import { enUS, ru, hy } from 'date-fns/locale'
import type { SupportedLocale } from '../../../i18n'
import { useActivityStore } from '../stores/activityStore'
import type { ActivityEventType } from '../../../types/activityevent'

const { t, locale } = useI18n()
const activity = useActivityStore()

/** date-fns locale objects, keyed by this app's locale codes. */
const dateFnsLocales: Record<SupportedLocale, Locale> = { en: enUS, ru, hy }

/** Loads the full feed (more than the bell's 10-item preview) on mount. */
onMounted(() => {
  activity.fetchActivity(50)
})

/**
 * Formats an ISO timestamp as a localized relative string.
 *
 * @param iso - ISO 8601 timestamp.
 * @returns Human-readable relative time in the active locale.
 */
function relativeTime(iso: string): string {
  return formatDistanceToNowStrict(new Date(iso), {
    addSuffix: true,
    locale: dateFnsLocales[locale.value as SupportedLocale],
  })
}

/** Icon path and color per event type, so the list reads at a glance. */
const typeStyle: Record<ActivityEventType, { icon: string; color: string }> = {
  ClientRegistered: {
    icon: 'M16 21v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2M9 11a4 4 0 100-8 4 4 0 000 8zM23 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75',
    color: 'text-status-green',
  },
  InvoiceOverdue: {
    icon: 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z',
    color: 'text-status-red',
  },
  DomainExpiring: {
    icon: 'M12 2a15.3 15.3 0 000 20 15.3 15.3 0 000-20zM2 12h20',
    color: 'text-status-yellow',
  },
}

const events = computed(() => activity.events)
</script>

<template>
  <div class="w-full">
    <h1 class="text-xl font-display font-semibold text-text-primary mb-1">{{ t('common.notifications') }}</h1>
    <p class="text-[0.85rem] text-text-muted mb-6">{{ t('common.viewAllNotifications') }}</p>

    <div v-if="activity.loading && events.length === 0" class="text-center py-12 text-[0.85rem] text-text-muted">
      {{ t('common.loading') }}
    </div>
    <div v-else-if="events.length === 0" class="text-center py-12 text-[0.85rem] text-text-muted">
      {{ t('common.noNotifications') }}
    </div>
    <div v-else class="w-full bg-surface-card border border-border rounded-2xl divide-y divide-border overflow-hidden">
      <div
        v-for="(event, index) in events"
        :key="`${event.type}-${event.occurredAt}-${index}`"
        class="flex items-start gap-3 px-4 py-3.5"
      >
        <svg
          class="w-4 h-4 mt-0.5 shrink-0"
          :class="typeStyle[event.type].color"
          viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"
        >
          <path :d="typeStyle[event.type].icon" />
        </svg>
        <div class="min-w-0">
          <p class="text-[0.85rem] text-text-primary">{{ event.message }}</p>
          <p class="text-[0.72rem] text-text-muted">{{ relativeTime(event.occurredAt) }}</p>
        </div>
      </div>
    </div>
  </div>
</template>
