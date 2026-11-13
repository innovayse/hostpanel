<script setup lang="ts">
/**
 * The four fact tiles at the top of an invoice: invoice date, due date, amount and the balance still owed.
 *
 * The balance is what the API reports as `amountDue` (it owns the rule: the total less the recorded payments,
 * nothing for a Paid, Refunded or Cancelled invoice). Without an invoice (still being read) the tiles draw the
 * captions with the kit's placeholder bars in place of the values, so the summary tab and the page's loading state share one layout.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import type { InvoiceDetail } from '@/types/invoice'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for InvoiceFactTiles. */
const props = defineProps<{
  /** The invoice whose facts are shown; null while it is being read. */
  invoice: InvoiceDetail | null
}>()

/** One tile of the grid. */
interface FactTile {
  /** Stable key of the tile; part of its test id. */
  key: string
  /** Tile caption. */
  label: string
  /** Tile value, empty while the invoice is being read. */
  value: string
  /** Tone of the value. */
  tone: UiTone
}

/** The tiles, in display order. */
const tiles = computed<FactTile[]>(() => {
  const invoice = props.invoice
  return [
    { key: 'invoiceDate', label: t('shared.invoiceDate'), value: invoice ? formatDate(invoice.dates.invoiceDate) : '', tone: UiTone.Neutral },
    { key: 'dueDate', label: t('shared.dueDate'), value: invoice ? formatDate(invoice.dates.dueDate) : '', tone: UiTone.Neutral },
    { key: 'amount', label: t('billing.invoiceSummaryTab.invoiceAmount'), value: invoice ? formatPlainAmount(invoice.amounts.total ?? 0, invoice.currency) : '', tone: UiTone.Neutral },
    {
      key: 'balance',
      label: t('shared.balance'),
      value: invoice ? formatPlainAmount(invoice.amountDue, invoice.currency) : '',
      tone: invoice && invoice.amountDue > 0 ? UiTone.Danger : UiTone.Success,
    },
  ]
})
</script>

<template>
  <UiGrid :columns="4" gap="md">
    <UiStatTile
      v-for="tile in tiles"
      :key="tile.key"
      :label="tile.label"
      :value="tile.value"
      :tone="tile.tone"
      :loading="invoice === null"
      :data-testid="`billing-invoice-fact-${tile.key}`"
    />
  </UiGrid>
</template>
