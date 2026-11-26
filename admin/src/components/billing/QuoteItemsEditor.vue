<script setup lang="ts">
/**
 * The line-item editor of a quote: quantity, description, unit price, discount and tax per line,
 * a running total per line, and the sub-total and total due. Used by the create and the detail
 * screens, which hold the lines as this component's `v-model`.
 *
 * It is the body of the line items card of `quoteSchemaParts`, which `UiDetailCards` draws on both screens (the card, its
 * title and its skeleton), so the editor draws no card or title of its own and heads its table from the schema's columns.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTableTotals from '@/components/ui/UiTableTotals.vue'
import type { UiTotalRow } from '@/components/ui/UiTableTotals.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { formatPlainAmount } from '@/lib/money'
import { blankQuoteLine } from '@/lib/quoteLines'
import { QUOTE_ITEMS_TABLE } from '@/lib/quoteSchemaParts'
import type { QuoteLineDraft } from '@/types/quote'
import { MAX_PERCENT } from '@/lib/limits'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiAlign } from '@/utils/ui/table'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { Icon } from '@/lib/icons'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

const props = withDefaults(defineProps<{
  /** ISO 4217 code the quote is priced in; amounts read in dollars while it is not known yet. */
  currency?: string | null
}>(), {
  currency: undefined,
})

/** The quote lines; the editor replaces the array on every change. */
const lines = defineModel<QuoteLineDraft[]>({ required: true })

/**
 * Computes what one line comes to after its discount.
 *
 * @param line - The line.
 * @returns Quantity times unit price, less the discount percentage.
 */
const lineTotal = (line: QuoteLineDraft): number => {
  const base = line.quantity * line.unitPrice
  return base - base * (line.discountPercent / MAX_PERCENT)
}

/** Sum of every line total (this is also the total due: quotes carry no separate tax line here). */
const subTotal = computed<number>(() => lines.value.reduce((sum, line) => sum + lineTotal(line), 0))

/** The totals under the lines: the sub-total and the total due (a quote carries no separate tax line here). */
const totalRows = computed<UiTotalRow[]>(() => [
  { key: 'subtotal', label: t('shared.subTotal2'), value: formatPlainAmount(subTotal.value, props.currency), testid: 'billing-quote-subtotal' },
  { key: 'total', label: t('shared.totalDue2'), value: formatPlainAmount(subTotal.value, props.currency), strong: true, testid: 'billing-quote-total' },
])

/**
 * Replaces one line's fields, leaving the others as they are.
 *
 * @param index - Position of the line.
 * @param change - The fields to overwrite.
 */
const patch = (index: number, change: Partial<QuoteLineDraft>): void => {
  lines.value = lines.value.map((line, position) => (position === index ? { ...line, ...change } : line))
}

/** Appends a blank line. */
const addLine = (): void => {
  lines.value = [...lines.value, blankQuoteLine()]
}

/**
 * Removes one line.
 *
 * @param index - Position of the line.
 */
const removeLine = (index: number): void => {
  lines.value = lines.value.filter((_, position) => position !== index)
}

/**
 * The row actions of a quote line: remove it, offered only while more than one line is left so a quote
 * always keeps at least one line; none otherwise.
 *
 * @param index - The line position.
 * @returns The action list passed to UiRowActions.
 */
const lineActions = (index: number): UiMenuAction[] => {
  if (lines.value.length <= 1) {
    return []
  }
  return [
    {
      label: t('billing.invoiceItems.removeItem'),
      icon: Icon.Trash,
      testid: `billing-quote-item-remove-${index}`,
      danger: true,
      /**
       * Removes this line.
       *
       * @returns Nothing.
       */
      onSelect: (): void => {
        removeLine(index)
      },
    },
  ]
}
</script>

<template>
  <UiStack :gap="UiGap.Sm" data-testid="billing-quote-items">
    <UiTable :density="UiTableDensity.Compact" :empty="lines.length === 0" :colspan="7">
      <UiSchemaTableHead :columns="QUOTE_ITEMS_TABLE.columns" />
      <UiTableBody>
        <UiTableRow v-for="(line, index) in lines" :key="line.key">
          <UiTableCell>
            <UiNumberInput
              :model-value="line.quantity"
              :min="1"
              placeholder="1"
              :data-testid="`billing-quote-item-quantity-${index}`"
              @update:model-value="patch(index, { quantity: $event ?? 0 })"
            />
          </UiTableCell>
          <UiTableCell>
            <UiTextarea
              :model-value="line.description"
              rows="1"
              :placeholder="t('shared.description')"
              :data-testid="`billing-quote-item-description-${index}`"
              @update:model-value="patch(index, { description: $event })"
            />
          </UiTableCell>
          <UiTableCell>
            <UiNumberInput
              :model-value="line.unitPrice"
              :step="0.01"
              :min="0"
              placeholder="0.00"
              :data-testid="`billing-quote-item-price-${index}`"
              @update:model-value="patch(index, { unitPrice: $event ?? 0 })"
            />
          </UiTableCell>
          <UiTableCell>
            <UiNumberInput
              :model-value="line.discountPercent"
              :step="1"
              :min="0"
              :max="MAX_PERCENT"
              placeholder="0"
              @update:model-value="patch(index, { discountPercent: $event ?? 0 })"
            />
          </UiTableCell>
          <UiTableCell :align="UiAlign.Center">
            <UiCheckbox
              :model-value="line.taxed"
              :aria-label="t('shared.taxed')"
              @update:model-value="patch(index, { taxed: $event })"
            />
          </UiTableCell>
          <UiTableCell numeric>
            <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ formatPlainAmount(lineTotal(line), props.currency) }}</UiText>
          </UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="lineActions(index)" :testid="`billing-quote-item-actions-${index}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <UiTableTotals :rows="totalRows" />
    </UiTable>

    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" wrap>
      <UiButton :action="UiActionKind.AddRow" :variant="UiButtonVariant.Secondary" :size="UiSize.Sm" data-testid="billing-quote-item-add" @click="addLine">
        {{ t('billing.actions.addItem') }}
      </UiButton>
    </UiStack>
  </UiStack>
</template>
