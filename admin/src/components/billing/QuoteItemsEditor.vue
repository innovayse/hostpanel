<script setup lang="ts">
/**
 * The line-item editor of a quote: quantity, description, unit price, discount and tax per line,
 * a running total per line, and the sub-total and total due. Used by the create and the detail
 * screens, which hold the lines as this component's `v-model`.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { Icon } from '@/lib/icons'
import { formatPlainAmount } from '@/lib/money'
import type { QuoteItemUpdate } from '@/types/quote'
import { MAX_PERCENT } from '@/lib/limits'

const { t } = useI18n()

const props = defineProps<{
  /** ISO 4217 code the quote is priced in; amounts read in dollars while it is not known yet. */
  currency?: string | null
}>()

/** The quote lines; the editor replaces the array on every change. */
const lines = defineModel<QuoteItemUpdate[]>({ required: true })

/**
 * Computes what one line comes to after its discount.
 *
 * @param line - The line.
 * @returns Quantity times unit price, less the discount percentage.
 */
const lineTotal = (line: QuoteItemUpdate): number => {
  const base = line.quantity * line.unitPrice
  return base - base * (line.discountPercent / MAX_PERCENT)
}

/** Sum of every line total (this is also the total due: quotes carry no separate tax line here). */
const subTotal = computed<number>(() => lines.value.reduce((sum, line) => sum + lineTotal(line), 0))

/**
 * Replaces one line's fields, leaving the others as they are.
 *
 * @param index - Position of the line.
 * @param change - The fields to overwrite.
 */
const patch = (index: number, change: Partial<QuoteItemUpdate>): void => {
  lines.value = lines.value.map((line, position) => (position === index ? { ...line, ...change } : line))
}

/** Appends a blank line. */
const addLine = (): void => {
  lines.value = [...lines.value, { quantity: 1, description: '', unitPrice: 0, discountPercent: 0, taxed: false }]
}

/**
 * Removes one line.
 *
 * @param index - Position of the line.
 */
const removeLine = (index: number): void => {
  lines.value = lines.value.filter((_, position) => position !== index)
}
</script>

<template>
  <UiCard padding="md" data-testid="billing-quote-items">
    <UiStack gap="sm">
      <UiHeading :level="3" variant="card">
        {{ t('shared.lineItems') }}
      </UiHeading>

      <UiTable density="compact" :empty="lines.length === 0" :colspan="7">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.qty') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.quoteDetail.unitPrice') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.quoteDetail.discount') }}</UiTableHeader>
            <UiTableHeader align="center">{{ t('shared.taxed') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('shared.total') }}</UiTableHeader>
            <UiTableHeader />
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="(line, index) in lines" :key="index">
            <UiTableCell>
              <UiNumberInput
                :model-value="line.quantity"
                :min="1"
                placeholder="1"
                :data-testid="`billing-quote-item-quantity-${index}`"
                @update:model-value="patch(index, { quantity: $event })"
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
                @update:model-value="patch(index, { unitPrice: $event })"
              />
            </UiTableCell>
            <UiTableCell>
              <UiNumberInput
                :model-value="line.discountPercent"
                :step="1"
                :min="0"
                :max="MAX_PERCENT"
                placeholder="0"
                @update:model-value="patch(index, { discountPercent: $event })"
              />
            </UiTableCell>
            <UiTableCell align="center">
              <UiCheckbox
                :model-value="line.taxed"
                :aria-label="t('shared.taxed')"
                @update:model-value="patch(index, { taxed: $event })"
              />
            </UiTableCell>
            <UiTableCell align="right">
              <UiText tone="primary" size="md">{{ formatPlainAmount(lineTotal(line), props.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell align="right">
              <UiIconButton
                v-if="lines.length > 1"
                :label="t('billing.addQuote.deleteRow')"
                :data-testid="`billing-quote-item-remove-${index}`"
                @click="removeLine(index)"
              >
                <UiIcon :markup="Icon.Close" size="sm" />
              </UiIconButton>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <UiStack direction="row" justify="between" align="center" wrap>
        <UiButton variant="secondary" size="sm" data-testid="billing-quote-item-add" @click="addLine">
          {{ t('billing.quoteDetail.addItem') }}
        </UiButton>
        <UiStack align="end" gap="xs">
          <UiStack direction="row" gap="lg" align="center">
            <UiText tone="muted" size="md">{{ t('shared.subTotal2') }}</UiText>
            <UiText tone="secondary" size="md" data-testid="billing-quote-subtotal">{{ formatPlainAmount(subTotal, props.currency) }}</UiText>
          </UiStack>
          <UiStack direction="row" gap="lg" align="center">
            <UiText tone="primary" size="md">{{ t('shared.totalDue2') }}</UiText>
            <UiText tone="primary" size="md" data-testid="billing-quote-total">{{ formatPlainAmount(subTotal, props.currency) }}</UiText>
          </UiStack>
        </UiStack>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
