<template>
  <UiCard data-testid="credit-history">
    <UiCardHeader :title="$t('client.credit.history.title')" />

    <UiEmptyState
      v-if="entries.length === 0"
      :icon="History"
      :title="$t('client.credit.history.empty')"
      :description="$t('client.credit.history.emptyHint')"
      class="!py-6"
      data-testid="credit-history-empty"
    />

    <UiTable v-else data-testid="credit-history-table">
      <UiTableHead>
        <UiTableRow :hoverable="false">
          <UiTableTh>{{ $t('client.credit.history.colDate') }}</UiTableTh>
          <UiTableTh>{{ $t('client.credit.history.colType') }}</UiTableTh>
          <UiTableTh>{{ $t('client.credit.history.colInvoice') }}</UiTableTh>
          <UiTableTh align="right">{{ $t('client.credit.history.colChange') }}</UiTableTh>
          <UiTableTh align="right">{{ $t('client.credit.history.colBalance') }}</UiTableTh>
        </UiTableRow>
      </UiTableHead>
      <UiTableBody>
        <UiTableRow v-for="entry in entries" :key="entry.id" :data-testid="`credit-entry-${entry.id}`">
          <UiTableTd>
            <UiText as="span" size="sm" :tone="UiTextTone.Subtle">{{ formatDateTime(entry.date) }}</UiText>
          </UiTableTd>
          <UiTableTd>
            <UiText as="span" size="sm" :data-testid="`credit-entry-type-${entry.id}`">{{ $t(`client.credit.movement.${entry.movement}`) }}</UiText>
          </UiTableTd>
          <UiTableTd>
            <UiLink v-if="entry.invoiceId !== null" :to="clientInvoicePath(entry.invoiceId)" :data-testid="`credit-entry-invoice-${entry.id}`">
              #{{ entry.invoiceId }}
            </UiLink>
            <UiText v-else as="span" size="sm" :tone="UiTextTone.Subtle">{{ EMPTY_DATE }}</UiText>
          </UiTableTd>
          <UiTableTd align="right">
            <UiText
              as="span"
              size="sm"
              weight="semibold"
              :tone="entry.change >= 0 ? UiTextTone.Success : UiTextTone.Danger"
              :data-testid="`credit-entry-change-${entry.id}`"
            >
              {{ signed(entry.change) }}
            </UiText>
          </UiTableTd>
          <UiTableTd align="right">
            <UiText as="span" size="sm" :data-testid="`credit-entry-balance-${entry.id}`">{{ money(entry.balanceAfter) }}</UiText>
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * HistoryTable. The client's credit history: every movement of the balance with its type, the invoice it concerned, the signed
 * change and the balance it left, newest first as the backend sent it.
 *
 * Nothing is computed here: the backend records the change and the balance after it, so the table only formats them.
 */
import { History } from 'lucide-vue-next'
import { clientInvoicePath } from '~/constants/clientRoutes'
import type { ClientCreditEntry } from '~/types/clientcreditentry'
import { formatCurrency } from '~/utils/formatCurrency'
import { EMPTY_DATE, formatDateTime } from '~/utils/formatDate'
import { UiTextTone } from '~/utils/ui/enums'

/** Props of HistoryTable. */
interface Props {
  /** The page of history lines to show. */
  entries: ClientCreditEntry[]
  /** ISO 4217 code the amounts are in. */
  currency: string
}

/** The props of the component. */
const props = defineProps<Props>()

/**
 * Formats an amount in the credit's currency.
 *
 * @param amount - The amount.
 * @returns The formatted amount.
 */
const money = (amount: number): string => formatCurrency(amount, { code: props.currency })

/**
 * Formats a signed change, always with its sign so an addition and a deduction read apart.
 *
 * @param change - The signed change.
 * @returns The formatted change, led by a plus for an addition.
 */
const signed = (change: number): string => (change > 0 ? `+${money(change)}` : money(change))
</script>
