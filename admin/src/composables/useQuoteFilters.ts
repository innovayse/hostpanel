/**
 * The quote filters every quote list shares: stage and validity.
 *
 * Both quote lists (the admin list and the tab of a client's profile) keep these two filters in the address through
 * `useListQuery`. This reads them back as the typed values the API takes, and describes them as chips, so the two
 * screens do not each carry the same conversion. The admin list adds its client filter next to them.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ListChipSource, ListQuery } from '@/composables/useListQuery'
import { QUOTE_STAGE_OPTIONS, QUOTE_VALIDITY_OPTIONS } from '@/lib/constants'
import { statusLabel } from '@/lib/tones'
import { QuoteStage, QuoteValidity } from '@/types/quote'
import type { QuoteListFilters } from '@/types/quote'

/** The quote filter fields every quote list has; each is one query key and one `QuoteListFilters` member. */
export const QuoteFilterKey = {
  Stage: 'stage',
  Validity: 'validity',
} as const

/** One of the {@link QuoteFilterKey} values. */
export type QuoteFilterKey = (typeof QuoteFilterKey)[keyof typeof QuoteFilterKey]

/** What {@link useQuoteFilters} hands the quote list screen. */
export interface QuoteFilterSet {
  /** The applied stage and validity as the API takes them; a member is undefined when its filter is not set. */
  filters: ComputedRef<QuoteListFilters>
  /**
   * Describes the applied quote filters for `chipsOf`.
   *
   * @returns One entry per quote filter, with the value as the operator reads it, undefined when it is not set.
   */
  chipSources: () => ListChipSource<QuoteFilterKey>[]
}

/**
 * Reads the quote filters from the address of a quote list.
 *
 * Must run in a component's setup, because it reads the locale.
 *
 * @param query - The list's address-backed filters; it must hold at least the {@link QuoteFilterKey} fields.
 * @returns The applied filters and their chip descriptions.
 */
export const useQuoteFilters = (query: Pick<ListQuery<QuoteFilterKey>, 'enumOf'>): QuoteFilterSet => {
  const { t } = useI18n()

  /** The stage in the address as a quote stage, undefined when none is set or the value is not one. */
  const stage = query.enumOf(QuoteFilterKey.Stage, QuoteStage)

  /** The validity in the address as a quote validity, undefined when none is set or the value is not one. */
  const validity = query.enumOf(QuoteFilterKey.Validity, QuoteValidity)

  return {
    /** The quote filters the address names, ready for the list request. */
    filters: computed(() => ({ stage: stage.value, validity: validity.value })),
    /**
     * The stage and validity chips.
     *
     * @returns Each filter with its key, label and the value the address holds.
     */
    chipSources: () => [
      {
        key: QuoteFilterKey.Stage,
        label: t('shared.stage'),
        value: stage.value === undefined ? undefined : statusLabel(QUOTE_STAGE_OPTIONS, stage.value),
      },
      {
        key: QuoteFilterKey.Validity,
        label: t('billing.quotesList.validityPeriod'),
        value: validity.value === undefined ? undefined : statusLabel(QUOTE_VALIDITY_OPTIONS, validity.value),
      },
    ],
  }
}
