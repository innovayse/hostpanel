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
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatCurrencyCode } from '@/lib/money'
import { placeholderRows } from '@/lib/placeholderRows'
import { AppRoute, invoiceViewPath } from '@/lib/routes'
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'
import { invoiceStatusTone, statusLabel } from '@/lib/tones'
import type { Invoice } from '@/types/invoice'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding, UiCardRadius } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap, UiJustify, UiStackTag } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant, UiTextTag, UiTextTone } from '@/utils/ui/text'

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

/** Classes of the row: one line from `sm` up, a name line over a number, total and status line on a phone. */
const ROW_CLASSES = 'border-t border-border-subtle text-sm max-sm:flex-wrap max-sm:gap-x-3 max-sm:gap-y-1 max-sm:py-2.5 sm:h-12'

/** Classes of the number cell: its own width on a phone, the first column from `sm` up. */
const NUMBER_CLASSES = 'min-w-0 max-sm:shrink-0 sm:flex-[1.2_1_0%]'

/** Classes of the client name cell: it takes the whole first line on a phone, the second column from `sm` up. */
const NAME_CLASSES = 'min-w-0 max-sm:order-first max-sm:basis-full sm:flex-[1.6_1_0%]'

/** Classes of the total cell: it takes the room left on the second phone line, the third column from `sm` up. */
const TOTAL_CLASSES = 'min-w-0 max-sm:flex-1 sm:flex-[1_1_0%]'

/** Classes of the status cell: its own width on a phone, the last column from `sm` up. */
const STATUS_CLASSES = 'min-w-0 max-sm:shrink-0 sm:flex-[0.8_1_0%]'

/** True until the first read has answered: the rows are drawn with placeholders. */
const initialLoading = computed<boolean>(() => props.loading && props.invoices.length === 0)

/** The rows of the list: the invoices, or null placeholders while they load. */
const rows = computed(() => placeholderRows(props.invoices, initialLoading.value, PLACEHOLDER_ROWS))
</script>

<template>
  <UiCard :padding="UiCardPadding.None" :radius="UiCardRadius.Panel" :aria-busy="initialLoading ? 'true' : undefined" class="p-5.5" data-testid="dashboard-recent-invoices">
    <template #header>
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between" class="mb-2.5">
        <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ t('dashboard.recentInvoices') }}</UiHeading>
        <UiLink :to="AppRoute.BillingInvoices" :variant="UiButtonVariant.Link" :size="UiSize.Sm" data-testid="dashboard-recent-invoices-all">
          {{ t('shared.viewAll') }}
        </UiLink>
      </UiStack>
    </template>
    <UiEmptyState v-if="!initialLoading && invoices.length === 0" compact :title="t('dashboard.noInvoices')" data-testid="dashboard-recent-invoices-empty" />
    <UiStack v-else :as="UiStackTag.Ul" :gap="UiGap.None">
      <UiStack
        v-for="(invoice, index) in rows"
        :key="invoice?.id ?? `placeholder-${index}`"
        :as="UiStackTag.Li"
        :direction="UiDirection.Row"
        :align="UiAlignItems.Center"
        :gap="UiGap.None"
        :class="ROW_CLASSES"
      >
        <UiStack :align="UiAlignItems.Start" :gap="UiGap.None" :class="NUMBER_CLASSES">
          <UiSkeletonBar v-if="invoice === null" class="h-3.5 w-10 rounded-md" aria-hidden="true" />
          <UiLink v-else :to="invoiceViewPath(invoice.id)" :variant="UiButtonVariant.Link" :size="UiSize.Sm" :data-testid="`dashboard-invoice-${invoice.id}`">#{{ invoice.id }}</UiLink>
        </UiStack>
        <UiStack :gap="UiGap.None" :class="NAME_CLASSES">
          <UiSkeletonBar v-if="invoice === null" class="h-3.5 w-3/4 rounded-md" aria-hidden="true" />
          <UiText v-else :as="UiTextTag.Span" :tone="UiTextTone.Plain" class="truncate pr-2">{{ invoice.clientName }}</UiText>
        </UiStack>
        <UiStack :align="UiAlignItems.Start" :gap="UiGap.None" :class="TOTAL_CLASSES">
          <UiSkeletonBar v-if="invoice === null" class="h-3.5 w-16 rounded-md" aria-hidden="true" />
          <UiText v-else :as="UiTextTag.Span" :tone="UiTextTone.Plain" class="tabular-nums">{{ formatCurrencyCode(invoice.total, invoice.currency ?? '') }}</UiText>
        </UiStack>
        <UiStack :align="UiAlignItems.Start" :gap="UiGap.None" :class="STATUS_CLASSES">
          <UiSkeletonBar v-if="invoice === null" class="h-5 w-16 rounded-full" aria-hidden="true" />
          <UiBadge v-else :tone="invoiceStatusTone(invoice.status)">{{ statusLabel(INVOICE_STATUS_OPTIONS, invoice.status) }}</UiBadge>
        </UiStack>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
