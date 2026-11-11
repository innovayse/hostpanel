/**
 * Pure rules of the email campaign form: what a value of each audience filter must look like,
 * and the conversion between the rows the form edits and the segments the API takes. The
 * API repeats these checks; the form only uses them to hold back a count or a save that could
 * not succeed.
 */
import { CampaignFilterType } from '@/types/emailcampaign'
import type { EmailCampaignSegment, EmailCampaignSegmentDraft } from '@/types/emailcampaign'

/** Fewest days ahead a service expiry rule looks. */
export const EXPIRING_DAYS_MIN = 1

/** Most days ahead a service expiry rule looks. */
export const EXPIRING_DAYS_MAX = 365

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
]

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
      return POSITIVE_INTEGER.test(trimmed)
    case CampaignFilterType.Country:
      return COUNTRY_CODE.test(trimmed)
    case CampaignFilterType.ServiceExpiringInDays:
      return POSITIVE_INTEGER.test(trimmed) && Number(trimmed) >= EXPIRING_DAYS_MIN && Number(trimmed) <= EXPIRING_DAYS_MAX
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
