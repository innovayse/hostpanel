<script setup lang="ts">
/**
 * The four fact tiles at the top of an invoice: invoice date, due date, amount and the balance still owed.
 *
 * The balance is what the API reports as `amountDue` (it owns the rule: the total less the recorded payments,
 * nothing for a Paid, Refunded or Cancelled invoice).
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import type { InvoiceDetail } from '@/types/invoice'
import { UiGap } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for InvoiceFactTiles. */
const props = defineProps<{
  /** The invoice whose facts are shown. */
  invoice: InvoiceDetail
}>()

/** One tile of the grid. */
interface FactTile {
  /** Stable key of the tile; part of its test id. */
  key: string
  /** Tile caption. */
  label: string
  /** Tile value. */
  value: string
  /** Tone of the value. */
  tone: UiTone
}

/** The tiles, in display order. */
const tiles = computed<FactTile[]>(() => {
  const invoice = props.invoice
  return [
    { key: 'invoiceDate', label: t('shared.invoiceDate'), value: formatDate(invoice.dates.invoiceDate), tone: UiTone.Neutral },
    { key: 'dueDate', label: t('shared.dueDate'), value: formatDate(invoice.dates.dueDate), tone: UiTone.Neutral },
    { key: 'amount', label: t('billing.invoiceSummaryTab.invoiceAmount'), value: formatPlainAmount(invoice.amounts.total, invoice.currency), tone: UiTone.Neutral },
    {
      key: 'balance',
      label: t('shared.balance'),
      value: formatPlainAmount(invoice.amountDue, invoice.currency),
      tone: invoice.amountDue > 0 ? UiTone.Danger : UiTone.Success,
    },
  ]
})
</script>

<template>
  <UiGrid :columns="4" :gap="UiGap.Md">
    <UiStatTile
      v-for="tile in tiles"
      :key="tile.key"
      :label="tile.label"
      :value="tile.value"
      :tone="tile.tone"
      :data-testid="`billing-invoice-fact-${tile.key}`"
    />
  </UiGrid>
</template>
