<script setup lang="ts">
/**
 * Latest invoices card of the dashboard: a row per invoice with its number, client, total and a status
 * pill, with a link to the full list. While the invoices load the title row stays and the same rows are drawn with
 * bars where the values go. From `sm` up a row is one line of four columns; on a phone it is two lines, the client
 * name alone on the first (so it keeps the whole width of the card) and the number, total and status under it.
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

/** Classes of the row: four columns from `sm` up, a name line over a number, total and status line on a phone. */
const ROW_CLASSES = 'grid items-center border-t border-border-subtle text-sm max-sm:grid-cols-[auto_1fr_auto] max-sm:gap-x-3 max-sm:gap-y-1 max-sm:py-2.5 sm:h-12 sm:grid-cols-[1.2fr_1.6fr_1fr_0.8fr]'

/** Classes of the client name cell: it takes the whole first line on a phone. */
const NAME_CLASSES = 'max-sm:col-span-3 max-sm:row-start-1'

/** Classes of the cells of the second phone line, which keep the second row whatever their order in the markup. */
const DETAIL_CLASSES = 'max-sm:row-start-2'

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
        :class="ROW_CLASSES"
      >
        <template v-if="invoice === null">
          <UiSkeletonBar :class="[DETAIL_CLASSES, 'h-3.5 w-10 rounded-md']" aria-hidden="true" />
          <UiSkeletonBar :class="[NAME_CLASSES, 'h-3.5 w-3/4 rounded-md']" aria-hidden="true" />
          <UiSkeletonBar :class="[DETAIL_CLASSES, 'h-3.5 w-16 rounded-md']" aria-hidden="true" />
          <UiSkeletonBar :class="[DETAIL_CLASSES, 'h-5 w-16 rounded-full']" aria-hidden="true" />
        </template>
        <template v-else>
          <UiLink :to="invoiceViewPath(invoice.id)" :class="[DETAIL_CLASSES, 'text-text-muted']" :data-testid="`dashboard-invoice-${invoice.id}`">#{{ invoice.id }}</UiLink>
          <span :class="[NAME_CLASSES, 'truncate pr-2']">{{ invoice.clientName }}</span>
          <span :class="[DETAIL_CLASSES, 'tabular-nums']">{{ formatCurrencyCode(invoice.total, invoice.currency ?? '') }}</span>
          <UiBadge :tone="invoiceStatusTone(invoice.status)" :class="[DETAIL_CLASSES, 'justify-self-start']">{{ invoice.status }}</UiBadge>
        </template>
      </li>
    </ul>
  </UiCard>
</template>
