/**
 * The invoice filters every invoice list shares: status and the creation period.
 *
 * Both invoice lists (the staff list and the tab of a client's profile) keep these filters in the address through
 * `useListQuery`. This reads them back as the typed values the API takes, and describes them as chips, so the two
 * screens do not each carry the same conversion. The staff list adds its client filter next to them.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ListChipSource, ListQuery } from '@/composables/useListQuery'
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { statusLabel } from '@/lib/tones'
import { InvoiceStatus } from '@/types/invoice'
import type { InvoiceListFilters } from '@/types/invoice'

/** The invoice filter fields every invoice list has; each is one query key and one `InvoiceListFilters` member. */
export const InvoiceFilterKey = {
  Status: 'status',
  From: 'from',
  To: 'to',
} as const

/** One of the {@link InvoiceFilterKey} values. */
export type InvoiceFilterKey = (typeof InvoiceFilterKey)[keyof typeof InvoiceFilterKey]

/** What {@link useInvoiceFilters} hands the invoice list screen. */
export interface InvoiceFilterSet {
  /** The applied status and period as the API takes them; a member is undefined when its filter is not set. */
  filters: ComputedRef<InvoiceListFilters>
  /**
   * Describes the applied invoice filters for `chipsOf`.
   *
   * @returns One entry per invoice filter, with the value as the operator reads it, undefined when it is not set.
   */
  chipSources: () => ListChipSource<InvoiceFilterKey>[]
}

/**
 * Reads the invoice filters from the address of an invoice list.
 *
 * Must run in a component's setup, because it reads the locale.
 *
 * @param query - The list's address-backed filters; it must hold at least the {@link InvoiceFilterKey} fields.
 * @returns The applied filters and their chip descriptions.
 */
export const useInvoiceFilters = (
  query: Pick<ListQuery<InvoiceFilterKey>, 'enumOf' | 'dateOf'>,
): InvoiceFilterSet => {
  const { t } = useI18n()

  /** The status in the address as an invoice status, undefined when none is set or the value is not one. */
  const status = query.enumOf(InvoiceFilterKey.Status, InvoiceStatus)

  /** The first creation day in the address, undefined when none is set or the value is not a date. */
  const from = query.dateOf(InvoiceFilterKey.From)

  /** The last creation day in the address, undefined when none is set or the value is not a date. */
  const to = query.dateOf(InvoiceFilterKey.To)

  return {
    /** The invoice filters the address names, ready for the list request. */
    filters: computed(() => ({ status: status.value, from: from.value, to: to.value })),
    /**
     * The status and period chips.
     *
     * @returns Each filter with its key, label and the value the address holds.
     */
    chipSources: () => [
      {
        key: InvoiceFilterKey.Status,
        label: t('shared.status'),
        value: status.value === undefined ? undefined : statusLabel(INVOICE_STATUS_OPTIONS, status.value),
      },
      {
        key: InvoiceFilterKey.From,
        label: t('billing.invoicesList.dateFrom'),
        value: from.value === undefined ? undefined : formatDate(from.value),
      },
      {
        key: InvoiceFilterKey.To,
        label: t('billing.invoicesList.dateTo'),
        value: to.value === undefined ? undefined : formatDate(to.value),
      },
    ],
  }
}
