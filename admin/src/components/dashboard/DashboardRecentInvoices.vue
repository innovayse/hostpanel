<script setup lang="ts">
/**
 * Latest invoices card of the dashboard: a row per invoice with its number, client, total and a status
 * pill, with a link to the full list. While the invoices load the title row stays and the same rows are drawn with
 * bars where the values go.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatCurrencyCode } from '@/lib/money'
import { placeholderRows } from '@/lib/placeholderRows'
import { AppRoute, invoiceViewPath } from '@/lib/routes'
import { invoiceStatusTone } from '@/lib/tones'
import type { Invoice } from '@/types/invoice'
import { UiCardRadius } from '@/utils/ui/card'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for DashboardRecentInvoices. */
const props = defineProps<{
  /** The newest invoices, newest first. */
  invoices: Invoice[]
  /** Whether the invoices are still loading. */
  loading: boolean
}>()

const { t } = useI18n()

/** How many placeholder rows the card draws while the invoices load (the dashboard asks for this many). */
const PLACEHOLDER_ROWS = 5

/** True until the first read has answered: the rows are drawn with placeholders. */
const initialLoading = computed<boolean>(() => props.loading && props.invoices.length === 0)

/** The rows of the list: the invoices, or null placeholders while they load. */
const rows = computed(() => placeholderRows(props.invoices, initialLoading.value, PLACEHOLDER_ROWS))
</script>

<template>
  <UiCard padding="none" :radius="UiCardRadius.Panel" :aria-busy="initialLoading ? 'true' : undefined" class="p-5.5" data-testid="dashboard-recent-invoices">
    <template #header>
      <UiStack direction="row" align="center" justify="between" class="mb-2.5">
        <UiHeading :level="2" variant="card">{{ t('dashboard.recentInvoices') }}</UiHeading>
        <UiLink :to="AppRoute.BillingInvoices" variant="link" size="sm" data-testid="dashboard-recent-invoices-all">
          {{ t('dashboard.viewAll') }}
        </UiLink>
      </UiStack>
    </template>
    <UiText v-if="!initialLoading && invoices.length === 0" :tone="UiTextTone.Muted" :size="UiTextSize.Md" class="py-3">{{ t('dashboard.noInvoices') }}</UiText>
    <ul v-else>
      <li
        v-for="(invoice, index) in rows"
        :key="invoice?.id ?? `placeholder-${index}`"
        class="grid h-12 grid-cols-[1.2fr_1.6fr_1fr_0.8fr] items-center border-t border-border-subtle text-sm"
      >
        <template v-if="invoice === null">
          <UiSkeletonBar class="h-3.5 w-10 rounded-md" aria-hidden="true" />
          <UiSkeletonBar class="h-3.5 w-3/4 rounded-md" aria-hidden="true" />
          <UiSkeletonBar class="h-3.5 w-16 rounded-md" aria-hidden="true" />
          <UiSkeletonBar class="h-5 w-16 rounded-full" aria-hidden="true" />
        </template>
        <template v-else>
          <UiLink :to="invoiceViewPath(invoice.id)" class="text-text-muted" :data-testid="`dashboard-invoice-${invoice.id}`">#{{ invoice.id }}</UiLink>
          <span class="truncate pr-2">{{ invoice.clientName }}</span>
          <span class="tabular-nums">{{ formatCurrencyCode(invoice.total, invoice.currency ?? '') }}</span>
          <UiBadge :tone="invoiceStatusTone(invoice.status)" class="justify-self-start">{{ invoice.status }}</UiBadge>
        </template>
      </li>
    </ul>
  </UiCard>
</template>
