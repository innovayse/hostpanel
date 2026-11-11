/**
 * Pure rules of the email campaign form: what a value of each audience filter must look like,
 * and the conversion between the rows the form edits and the segments the API takes. The
 * API repeats these checks; the form only uses them to hold back a count or a save that could
 * not succeed.
 */
import { CampaignFilterType } from '@/types/emailcampaign'
import type { CampaignDaysRange, EmailCampaignSegment, EmailCampaignSegmentDraft } from '@/types/emailcampaign'

/** The days each days-based rule accepts: how far ahead a service expiry looks, how long ago a sign-in may be. */
const DAYS_RANGES: Readonly<Partial<Record<CampaignFilterType, CampaignDaysRange>>> = {
  [CampaignFilterType.ServiceExpiringInDays]: { min: 1, max: 365 },
  [CampaignFilterType.NotSignedInForDays]: { min: 1, max: 3650 },
}

/** Longest subject the API accepts. */
export const CAMPAIGN_SUBJECT_MAX_LENGTH = 200

/** Longest body the API accepts. */
export const CAMPAIGN_BODY_MAX_LENGTH = 100_000

/** A positive whole number written without a sign or leading zero. */
const POSITIVE_INTEGER = /^[1-9]\d*$/

/** Two upper-case ASCII letters, the shape of an ISO 3166 alpha-2 country code. */
const COUNTRY_CODE = /^[A-Z]{2}$/

/** The filter types in the order the form offers them. */
export const CAMPAIGN_FILTER_TYPES: readonly CampaignFilterType[] = [
  CampaignFilterType.AllActive,
  CampaignFilterType.Product,
  CampaignFilterType.Country,
  CampaignFilterType.ServiceExpiringInDays,
  CampaignFilterType.ClientGroup,
  CampaignFilterType.NotSignedInForDays,
]

/**
 * The filter types an operator can pick, left without the sign-in rule where the deployment does not
 * know when a client signed in (the API refuses that rule there).
 *
 * @param signInKnown - Whether the deployment owns its accounts and so tracks the last sign-in.
 * @returns The types the form offers, in order.
 */
export const offeredFilterTypes = (signInKnown: boolean): readonly CampaignFilterType[] =>
  signInKnown ? CAMPAIGN_FILTER_TYPES : CAMPAIGN_FILTER_TYPES.filter((type) => type !== CampaignFilterType.NotSignedInForDays)

/**
 * The days a rule accepts, for the rules whose value is a number of days.
 *
 * @param filterType - The filter type.
 * @returns The range, or null for a rule whose value is not a number of days.
 */
export const daysRangeOf = (filterType: CampaignFilterType): CampaignDaysRange | null => DAYS_RANGES[filterType] ?? null

/**
 * Whether a filter type needs a value to be picked or typed.
 *
 * @param filterType - The filter type.
 * @returns False only for the type that matches every active client.
 */
export const filterTakesValue = (filterType: CampaignFilterType): boolean => filterType !== CampaignFilterType.AllActive

/**
 * Whether a value fits its filter type.
 *
 * @param filterType - The filter type.
 * @param value - The value as typed or picked.
 * @returns True when the rule can be sent to the API.
 */
export const isFilterValueValid = (filterType: CampaignFilterType, value: string): boolean => {
  const trimmed = value.trim()
  switch (filterType) {
    case CampaignFilterType.AllActive:
      return trimmed === ''
    case CampaignFilterType.Product:
    case CampaignFilterType.ClientGroup:
      return POSITIVE_INTEGER.test(trimmed)
    case CampaignFilterType.Country:
      return COUNTRY_CODE.test(trimmed)
    case CampaignFilterType.ServiceExpiringInDays:
    case CampaignFilterType.NotSignedInForDays: {
      const range = daysRangeOf(filterType)
      return range !== null && POSITIVE_INTEGER.test(trimmed) && Number(trimmed) >= range.min && Number(trimmed) <= range.max
    }
    default:
      return false
  }
}

/**
 * Whether every row of the audience is filled in and there is at least one.
 *
 * @param drafts - The rows of the form.
 * @returns True when the rows can be counted and saved.
 */
export const areSegmentsComplete = (drafts: readonly EmailCampaignSegmentDraft[]): boolean =>
  drafts.length > 0 && drafts.every((draft) => isFilterValueValid(draft.filterType, draft.filterValue))

/**
 * Builds a blank row.
 *
 * @param key - The stable key of the new row.
 * @param filterType - The rule the row starts with.
 * @returns The row, with no value.
 */
export const newSegmentDraft = (
  key: number,
  filterType: CampaignFilterType = CampaignFilterType.AllActive,
): EmailCampaignSegmentDraft => ({ key, filterType, filterValue: '' })

/**
 * Turns the segments of a loaded campaign into form rows.
 *
 * @param segments - The segments the API returned.
 * @param firstKey - The key of the first row; the next rows count up from it.
 * @returns The rows, in the same order.
 */
export const draftsFromSegments = (segments: readonly EmailCampaignSegment[], firstKey: number): EmailCampaignSegmentDraft[] =>
  segments.map((segment, index) => ({
    key: firstKey + index,
    filterType: segment.filterType,
    filterValue: segment.filterValue ?? '',
  }))

/**
 * Turns the form rows into the segments the API takes.
 *
 * @param drafts - The rows of the form.
 * @returns The segments; a rule with no value carries `null`.
 */
export const segmentsFromDrafts = (drafts: readonly EmailCampaignSegmentDraft[]): EmailCampaignSegment[] =>
  drafts.map((draft) => ({
    filterType: draft.filterType,
    filterValue: filterTakesValue(draft.filterType) ? draft.filterValue.trim() : null,
  }))
