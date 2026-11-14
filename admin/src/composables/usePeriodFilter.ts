/**
 * The period filter every billing list with a date range shares: the first and the last day read back from the address,
 * and the two chips that describe them. The invoice lists and the transaction lists each name their own `from` and `to`
 * keys; the days, the labels and the display format are written here once.
 */
import { useI18n } from 'vue-i18n'
import type { ComputedRef } from 'vue'
import type { ListChipSource, ListQuery } from '@/composables/useListQuery'
import { formatDate } from '@/lib/format'

/** What {@link usePeriodFilter} hands the filter composable of a list. */
export interface PeriodFilter<K extends string> {
  /** The first day in the address as `YYYY-MM-DD`, undefined when none is set or the value is not a day. */
  from: ComputedRef<string | undefined>
  /** The last day in the address as `YYYY-MM-DD`, undefined when none is set or the value is not a day. */
  to: ComputedRef<string | undefined>
  /**
   * Describes the applied period for `chipsOf`.
   *
   * @returns The "from" and "to" entries, with the value as the operator reads it, undefined when it is not set.
   */
  chipSources: () => ListChipSource<K>[]
}

/**
 * Reads the period filter from the address of a list.
 *
 * Must run in a component's setup, because it reads the locale.
 *
 * @param query - The list's address-backed filters.
 * @param keys - The address keys that hold the first and the last day.
 * @param keys.from - The address key of the first day.
 * @param keys.to - The address key of the last day.
 * @returns The days and their chip descriptions.
 */
export const usePeriodFilter = <K extends string>(
  query: Pick<ListQuery<K>, 'dateOf'>,
  keys: { from: K; to: K },
): PeriodFilter<K> => {
  const { t } = useI18n()

  /** The first day in the address. */
  const from = query.dateOf(keys.from)

  /** The last day in the address. */
  const to = query.dateOf(keys.to)

  return {
    from,
    to,
    /**
     * The period chips.
     *
     * @returns Each end with its key, label and the value the address holds.
     */
    chipSources: () => [
      { key: keys.from, label: t('shared.dateFrom'), value: from.value === undefined ? undefined : formatDate(from.value) },
      { key: keys.to, label: t('shared.dateTo'), value: to.value === undefined ? undefined : formatDate(to.value) },
    ],
  }
}
