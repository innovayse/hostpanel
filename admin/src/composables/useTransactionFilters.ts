/**
 * The transaction filters every transaction list shares: direction, payment method, period and amount.
 *
 * Both transaction lists (the ledger and the tab of a client's profile) keep these filters in the address through
 * `useListQuery`. This reads them back as the typed values the API takes, and describes them as chips, so the two
 * screens do not each carry the same conversion. The ledger adds its client filter next to them.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ListChipSource, ListQuery } from '@/composables/useListQuery'
import { TRANSACTION_DIRECTION_OPTIONS, TRANSACTION_METHOD_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { statusLabel } from '@/lib/tones'
import { TransactionDirection } from '@/types/transaction'
import type { TransactionListFilters } from '@/types/transaction'

/** The transaction filter fields every transaction list has; each is one query key and one `TransactionListFilters` member. */
export const TransactionFilterKey = {
  Direction: 'direction',
  PaymentMethod: 'paymentMethod',
  From: 'from',
  To: 'to',
  Amount: 'amount',
} as const

/** One of the {@link TransactionFilterKey} values. */
export type TransactionFilterKey = (typeof TransactionFilterKey)[keyof typeof TransactionFilterKey]

/** What {@link useTransactionFilters} hands the transaction list screen. */
export interface TransactionFilterSet {
  /** The applied direction, payment method, period and amount as the API takes them; a member is undefined when its filter is not set. */
  filters: ComputedRef<TransactionListFilters>
  /**
   * Describes the applied transaction filters for `chipsOf`.
   *
   * @returns One entry per transaction filter, with the value as the operator reads it, undefined when it is not set.
   */
  chipSources: () => ListChipSource<TransactionFilterKey>[]
}

/**
 * Reads the transaction filters from the address of a transaction list.
 *
 * Must run in a component's setup, because it reads the locale.
 *
 * @param query - The list's address-backed filters; it must hold at least the {@link TransactionFilterKey} fields.
 * @returns The applied filters and their chip descriptions.
 */
export const useTransactionFilters = (
  query: Pick<ListQuery<TransactionFilterKey>, 'enumOf' | 'dateOf' | 'applied'>,
): TransactionFilterSet => {
  const { t } = useI18n()

  /** The direction in the address as a transaction direction, undefined when none is set or the value is not one. */
  const direction = query.enumOf(TransactionFilterKey.Direction, TransactionDirection)

  /** The payment method in the address as written, undefined when none is set. */
  const paymentMethod = computed<string | undefined>(() => {
    const value = query.applied.value[TransactionFilterKey.PaymentMethod]
    return value === '' ? undefined : value
  })

  /** The first day in the address, undefined when none is set or the value is not a date. */
  const from = query.dateOf(TransactionFilterKey.From)

  /** The last day in the address, undefined when none is set or the value is not a date. */
  const to = query.dateOf(TransactionFilterKey.To)

  /** The amount in the address as a number, undefined when none is set or the value is not a positive number. */
  const amount = computed<number | undefined>(() => {
    const value = Number(query.applied.value[TransactionFilterKey.Amount])
    return Number.isFinite(value) && value > 0 ? value : undefined
  })

  return {
    /** The transaction filters the address names, ready for the list request. */
    filters: computed(() => ({
      direction: direction.value,
      paymentMethod: paymentMethod.value,
      from: from.value,
      to: to.value,
      amount: amount.value,
    })),
    /**
     * The direction, payment method, period and amount chips.
     *
     * @returns Each filter with its key, label and the value the address holds.
     */
    chipSources: () => [
      {
        key: TransactionFilterKey.Direction,
        label: t('billing.transactionsList.show'),
        value: direction.value === undefined ? undefined : statusLabel(TRANSACTION_DIRECTION_OPTIONS, direction.value),
      },
      {
        key: TransactionFilterKey.PaymentMethod,
        label: t('shared.paymentMethod'),
        value: paymentMethod.value === undefined ? undefined : statusLabel(TRANSACTION_METHOD_OPTIONS, paymentMethod.value),
      },
      {
        key: TransactionFilterKey.From,
        label: t('billing.invoicesList.dateFrom'),
        value: from.value === undefined ? undefined : formatDate(from.value),
      },
      {
        key: TransactionFilterKey.To,
        label: t('billing.invoicesList.dateTo'),
        value: to.value === undefined ? undefined : formatDate(to.value),
      },
      {
        key: TransactionFilterKey.Amount,
        label: t('shared.amount'),
        value: amount.value === undefined ? undefined : String(amount.value),
      },
    ],
  }
}
