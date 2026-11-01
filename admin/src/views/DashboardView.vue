<script setup lang="ts">
/**
 * Admin dashboard: a grid of KPI tiles with the platform's headline numbers, the latest events of
 * the activity feed and shortcuts to the screens operators open most.
 *
 * Reads `stores/dashboard` (a refusal shows the store's sentence in an alert) and `stores/activity`
 * (the feed card only appears once there is something to show).
 */
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiText from '@/components/ui/UiText.vue'
import { activityEventLink } from '@/lib/activityEventLink'
import { ACTIVITY_EVENT_LOOK } from '@/lib/activityEventLook'
import { formatRelativeFromNow } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { formatCurrencyWhole } from '@/lib/money'
import { AppRoute } from '@/lib/routes'
import { useActivityStore } from '@/stores/activity'
import { useDashboardStore } from '@/stores/dashboard'
import type { DashboardFigure } from '@/types/dashboardstats'
import { UiTone } from '@/utils/ui/tone'

/** One KPI tile of the dashboard. */
interface StatTileConfig {
  /** Field of `DashboardStats` the tile shows. */
  key: DashboardFigure
  /** Locale key of the tile caption. */
  labelKey: string
  /** Colour of the number. */
  tone: UiTone
  /** Inner SVG markup of the tile icon. */
  icon: string
  /** Turns the raw number, with the currency the revenue figures are in, into the text shown on the tile. */
  format: (value: number, currency: string) => string
}

/** One shortcut of the quick-links card. */
interface QuickLink {
  /** Locale key of the link caption. */
  labelKey: string
  /** Route the link opens. */
  to: string
  /** Inner SVG markup of the link icon. */
  icon: string
}

/** Dashboard store: the headline numbers. */
const store = useDashboardStore()

/** Activity store: the recent events shown beside the tiles. */
const activity = useActivityStore()

const { t, locale } = useI18n()

/** How many feed events the dashboard card previews; the notifications page shows the rest. */
const ACTIVITY_PREVIEW = 6

/**
 * Formats a count as plain text.
 *
 * @param value - The count.
 * @returns The count as a string.
 */
const formatCount = (value: number): string => value.toString()

/** The tiles, in display order. */
const statConfig: StatTileConfig[] = [
  { key: 'totalRevenue', labelKey: 'dashboard.stats.totalRevenue', tone: UiTone.Neutral, icon: Icon.Dollar, format: formatCurrencyWhole },
  { key: 'monthlyRevenue', labelKey: 'dashboard.stats.monthlyRevenue', tone: UiTone.Neutral, icon: Icon.Calendar, format: formatCurrencyWhole },
  { key: 'activeServices', labelKey: 'dashboard.stats.activeServices', tone: UiTone.Success, icon: Icon.CheckCircle, format: formatCount },
  { key: 'overdueInvoices', labelKey: 'dashboard.stats.overdueInvoices', tone: UiTone.Danger, icon: Icon.Alert, format: formatCount },
  { key: 'openTickets', labelKey: 'dashboard.stats.openTickets', tone: UiTone.Warning, icon: Icon.Chat, format: formatCount },
  { key: 'totalClients', labelKey: 'dashboard.stats.totalClients', tone: UiTone.Neutral, icon: Icon.Users, format: formatCount },
]

/** The shortcuts, in display order. */
const quickLinks: QuickLink[] = [
  { labelKey: 'nav.clients', to: AppRoute.Clients, icon: Icon.Users },
  { labelKey: 'nav.orders', to: AppRoute.Orders, icon: Icon.CheckCircle },
  { labelKey: 'nav.billingChildren.invoices', to: AppRoute.BillingInvoices, icon: Icon.Dollar },
  { labelKey: 'nav.supportChildren.tickets', to: AppRoute.SupportTickets, icon: Icon.Chat },
  { labelKey: 'nav.reports', to: AppRoute.Reports, icon: Icon.Calendar },
  { labelKey: 'nav.servers', to: AppRoute.Servers, icon: Icon.Globe },
]

onMounted(() => {
  void store.fetchStats()
  void activity.fetchActivity(ACTIVITY_PREVIEW)
})
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('dashboard.title')"
      :subtitle="t('dashboard.subtitle')"
      data-testid="dashboard-header"
    >
      <template #actions>
        <UiBadge tone="success" dot data-testid="dashboard-live">
          {{ t('dashboard.live') }}
        </UiBadge>
      </template>
    </UiPageHeader>

    <UiStack v-if="store.loading" aria-busy="true" data-testid="dashboard-loading">
      <UiText tone="secondary" size="md">
        {{ t('dashboard.loadingMetrics') }}
      </UiText>
      <UiSkeleton :lines="3" />
    </UiStack>

    <UiErrorState
      v-else-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="dashboard-error"
      @retry="store.fetchStats()"
    />

    <UiGrid v-else-if="store.stats" :columns="3" data-testid="dashboard-stats">
      <UiStatTile
        v-for="stat in statConfig"
        :key="stat.key"
        :label="t(stat.labelKey)"
        :value="stat.format(store.stats[stat.key], store.stats.currency)"
        :tone="stat.tone"
        :data-testid="`dashboard-stat-${stat.key}`"
      >
        <template #icon>
          <UiIcon :markup="stat.icon" />
        </template>
      </UiStatTile>
    </UiGrid>

    <UiGrid v-if="store.stats" :columns="2" data-testid="dashboard-panels">
      <UiCard v-if="activity.events.length > 0" padding="md" data-testid="dashboard-activity">
        <UiStack gap="sm">
          <!-- The link sits under the heading, so a long translated title never squeezes it. -->
          <UiStack gap="xs" align="start">
            <UiHeading :level="2" variant="section">{{ t('dashboard.recentActivity') }}</UiHeading>
            <UiLink :to="AppRoute.Notifications" variant="link" data-testid="dashboard-activity-all">
              {{ t('common.viewAll') }}
            </UiLink>
          </UiStack>
          <UiLink
            v-for="(event, index) in activity.events"
            :key="`${event.type}-${event.occurredAt}-${index}`"
            :to="activityEventLink(event)"
            class="flex items-start gap-3 rounded-lg px-2 py-2.5 transition-colors hover:bg-surface-hover"
            :data-testid="`dashboard-activity-${index}`"
          >
            <UiIcon :markup="ACTIVITY_EVENT_LOOK[event.type].icon" size="sm" :class="ACTIVITY_EVENT_LOOK[event.type].color" />
            <UiStack gap="xs">
              <UiText size="md">{{ event.message }}</UiText>
              <UiText tone="secondary" size="xs">{{ formatRelativeFromNow(event.occurredAt, locale) }}</UiText>
            </UiStack>
          </UiLink>
        </UiStack>
      </UiCard>

      <UiCard padding="md" class="@container" data-testid="dashboard-quick-links">
        <UiStack gap="sm">
          <UiHeading :level="2" variant="section">{{ t('dashboard.quickLinks') }}</UiHeading>
          <!-- Two columns only when the card is wide enough for the longest label; otherwise one column. -->
          <UiGrid :columns="1" gap="sm" class="@md:grid-cols-2">
            <UiLink
              v-for="link in quickLinks"
              :key="link.to"
              :to="link.to"
              class="flex items-center gap-3 rounded-xl border border-border bg-surface-field px-3 py-3 text-text-primary transition-colors hover:bg-surface-hover"
              :data-testid="`dashboard-link-${link.to.slice(1)}`"
            >
              <UiIcon :markup="link.icon" size="sm" />
              <UiText size="md">{{ t(link.labelKey) }}</UiText>
            </UiLink>
          </UiGrid>
        </UiStack>
      </UiCard>
    </UiGrid>
  </UiStack>
</template>
