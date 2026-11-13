/**
 * Vocabulary of the advanced-filter rows (`UiAdvancedFilters`): how a row's value is matched
 * against a field. Shared by the component and by the pure helper that narrows a returned page. Also the
 * shape of an active-filter chip (`UiFilterChips`).
 */

/** Closed set of ways a filter row matches its value. */
export const UiFilterCondition = {
  /** The field equals the value (ignoring case). */
  Exact: 'exact',
  /** The field contains the value (ignoring case). */
  Contains: 'contains',
} as const

/** One of the {@link UiFilterCondition} values. */
export type UiFilterCondition = (typeof UiFilterCondition)[keyof typeof UiFilterCondition]

/**
 * One active filter, shown as a removable chip beside the list.
 *
 * @typeParam K - The union of filter keys the screen owns, so `remove` reports exactly that type.
 */
export interface UiFilterChip<K extends string = string> {
  /** Key of the filter the chip stands for; what `remove` reports. */
  key: K
  /** Caption of the filter field (already translated), for example "Status". */
  label: string
  /** The chosen value as the operator reads it (already translated), for example "Active". */
  value: string
}
