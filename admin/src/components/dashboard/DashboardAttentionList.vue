<script setup lang="ts">
/**
 * "Needs attention" card of the dashboard: one row per kind of item waiting for staff (overdue invoices,
 * pending orders, domains expiring within 30 days, high priority open tickets), each with a glowing dot,
 * its name and how many wait, linking to the list that holds them. The title row stays while the counts load.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'
import { UiCardRadius } from '@/utils/ui/card'
import { UiTextTag, UiTextTone } from '@/utils/ui/text'

/** Kinds of item that need a person; also the locale key suffix of the row label. */
enum AttentionKind {
  /** An invoice past its due date. */
  OverdueInvoice = 'overdueInvoice',
  /** An order waiting for staff. */
  PendingOrder = 'pendingOrder',
  /** A domain about to expire. */
  ExpiringDomain = 'expiringDomain',
  /** An open ticket of high priority. */
  UrgentTicket = 'urgentTicket',
}

/** One row of the card. */
interface AttentionRow {
  /** What kind of item the row counts. */
  kind: AttentionKind
  /** How many of them wait. */
  count: number
  /** Route of the list that holds them. */
  to: string
  /** Colour classes of the dot: its colour and the glow of the same colour. */
  dot: string
}

/** Props for DashboardAttentionList. */
const props = defineProps<{
  /** Number of overdue invoices. */
  overdueInvoices: number
  /** Number of pending orders. */
  pendingOrders: number
  /** Number of domains expiring within 30 days. */
  expiringDomains: number
  /** Number of open tickets of high priority. */
  urgentTickets: number
  /** Whether the counts are still loading. */
  loading: boolean
}>()

const { t } = useI18n()

/** One row per kind, in the order the kinds are declared. */
const rows = computed<AttentionRow[]>(() => [
  { kind: AttentionKind.OverdueInvoice, count: props.overdueInvoices, to: AppRoute.BillingInvoices, dot: 'text-status-yellow' },
  { kind: AttentionKind.PendingOrder, count: props.pendingOrders, to: AppRoute.Orders, dot: 'text-status-red' },
  { kind: AttentionKind.ExpiringDomain, count: props.expiringDomains, to: AppRoute.BillingRenewals, dot: 'text-status-blue' },
  { kind: AttentionKind.UrgentTicket, count: props.urgentTickets, to: AppRoute.SupportTickets, dot: 'text-status-red' },
])
</script>

<template>
  <UiCard padding="none" :radius="UiCardRadius.Panel" class="p-5.5" data-testid="dashboard-attention">
    <UiStack gap="none" class="gap-3">
      <UiHeading :level="2" variant="card">{{ t('dashboard.attention.title') }}</UiHeading>
      <UiLink
        v-for="row in rows"
        :key="row.kind"
        :to="row.to"
        class="flex items-center gap-3 rounded-[14px] bg-surface-row p-3 text-text-primary transition-colors hover:bg-surface-hover"
        :data-testid="`dashboard-attention-${row.kind}`"
      >
        <UiText :as="UiTextTag.Span" :tone="UiTextTone.Plain" class="size-2.5 shrink-0 rounded-full bg-current shadow-[0_0_12px_currentColor]" :class="row.dot" aria-hidden="true" />
        <UiText :as="UiTextTag.Span" :tone="UiTextTone.Plain" class="flex-1 text-sm">{{ t(`dashboard.attention.kind.${row.kind}`) }}</UiText>
        <UiSkeletonBar v-if="loading" class="h-4 w-6 rounded-md" aria-hidden="true" />
        <UiText v-else :as="UiTextTag.Span" :tone="UiTextTone.Plain" class="font-display font-semibold" :data-testid="`dashboard-attention-${row.kind}-count`">{{ row.count }}</UiText>
      </UiLink>
    </UiStack>
  </UiCard>
</template>
