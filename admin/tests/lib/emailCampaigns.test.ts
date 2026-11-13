/**
 * Unit tests for the email campaign rules: what each audience filter accepts as a value, whether a
 * set of rows is complete, and the round trip between form rows and API segments.
 */
import { describe, expect, it } from 'vitest'
import {
  areSegmentsComplete,
  CAMPAIGN_FILTER_TYPES,
  daysRangeOf,
  draftsFromSegments,
  filterTakesValue,
  isFilterValueValid,
  newSegmentDraft,
  offeredFilterTypes,
  segmentKey,
  segmentsFromDrafts,
} from '@/lib/emailCampaigns'
import { CampaignFilterType } from '@/types/emailcampaign'

describe('CAMPAIGN_FILTER_TYPES', () => {
  it('offers every filter type once', () => {
    expect([...CAMPAIGN_FILTER_TYPES].sort()).toEqual(Object.values(CampaignFilterType).sort())
  })
})

describe('offeredFilterTypes', () => {
  it('offers every type where the last sign-in is known', () => {
    expect(offeredFilterTypes(true)).toEqual(CAMPAIGN_FILTER_TYPES)
  })

  it('leaves out the sign-in rule where the last sign-in is not known', () => {
    const offered = offeredFilterTypes(false)

    expect(offered).not.toContain(CampaignFilterType.NotSignedInForDays)
    expect(offered).toHaveLength(CAMPAIGN_FILTER_TYPES.length - 1)
  })
})

describe('daysRangeOf', () => {
  it('gives the days of the two days-based rules and nothing for the others', () => {
    expect(daysRangeOf(CampaignFilterType.ServiceExpiringInDays)).toEqual({ min: 1, max: 365 })
    expect(daysRangeOf(CampaignFilterType.NotSignedInForDays)).toEqual({ min: 1, max: 3650 })
    expect(daysRangeOf(CampaignFilterType.ClientGroup)).toBeNull()
    expect(daysRangeOf(CampaignFilterType.AllActive)).toBeNull()
  })
})

describe('filterTakesValue', () => {
  it('is false only for all active clients', () => {
    expect(filterTakesValue(CampaignFilterType.AllActive)).toBe(false)
    expect(filterTakesValue(CampaignFilterType.Product)).toBe(true)
    expect(filterTakesValue(CampaignFilterType.Country)).toBe(true)
    expect(filterTakesValue(CampaignFilterType.ServiceExpiringInDays)).toBe(true)
    expect(filterTakesValue(CampaignFilterType.ClientGroup)).toBe(true)
    expect(filterTakesValue(CampaignFilterType.NotSignedInForDays)).toBe(true)
  })
})

describe('isFilterValueValid', () => {
  it('takes no value for all active clients', () => {
    expect(isFilterValueValid(CampaignFilterType.AllActive, '')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.AllActive, '5')).toBe(false)
  })

  it('takes a positive product id', () => {
    expect(isFilterValueValid(CampaignFilterType.Product, '12')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.Product, ' 12 ')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.Product, '0')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.Product, '-3')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.Product, '1.5')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.Product, '')).toBe(false)
  })

  it('takes an upper-case two-letter country code', () => {
    expect(isFilterValueValid(CampaignFilterType.Country, 'AM')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.Country, 'am')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.Country, 'ARM')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.Country, '')).toBe(false)
  })

  it('takes 1 to 365 days', () => {
    expect(isFilterValueValid(CampaignFilterType.ServiceExpiringInDays, '1')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.ServiceExpiringInDays, '365')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.ServiceExpiringInDays, '0')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.ServiceExpiringInDays, '366')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.ServiceExpiringInDays, 'x')).toBe(false)
  })

  it('takes a positive client group id', () => {
    expect(isFilterValueValid(CampaignFilterType.ClientGroup, '4')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.ClientGroup, '0')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.ClientGroup, 'vip')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.ClientGroup, '')).toBe(false)
  })

  it('takes 1 to 3650 days without a sign-in', () => {
    expect(isFilterValueValid(CampaignFilterType.NotSignedInForDays, '1')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.NotSignedInForDays, '3650')).toBe(true)
    expect(isFilterValueValid(CampaignFilterType.NotSignedInForDays, '0')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.NotSignedInForDays, '3651')).toBe(false)
    expect(isFilterValueValid(CampaignFilterType.NotSignedInForDays, 'x')).toBe(false)
  })

  it('refuses a filter type this build does not know', () => {
    expect(isFilterValueValid('Newer' as CampaignFilterType, '')).toBe(false)
  })
})

describe('areSegmentsComplete', () => {
  it('needs at least one row', () => {
    expect(areSegmentsComplete([])).toBe(false)
  })

  it('needs every row to hold a valid value', () => {
    const ready = newSegmentDraft(1)
    const unfinished = { ...newSegmentDraft(2, CampaignFilterType.Product), filterValue: '' }

    expect(areSegmentsComplete([ready])).toBe(true)
    expect(areSegmentsComplete([ready, unfinished])).toBe(false)
    expect(areSegmentsComplete([ready, { ...unfinished, filterValue: '4' }])).toBe(true)
  })
})

describe('segment conversion', () => {
  it('builds a blank row with the key and filter it is given', () => {
    expect(newSegmentDraft(7)).toEqual({ key: 7, filterType: CampaignFilterType.AllActive, filterValue: '' })
    expect(newSegmentDraft(8, CampaignFilterType.Country)).toEqual({ key: 8, filterType: CampaignFilterType.Country, filterValue: '' })
  })

  it('turns API segments into rows keyed from the first key, null values becoming empty text', () => {
    const rows = draftsFromSegments(
      [
        { filterType: CampaignFilterType.AllActive, filterValue: null },
        { filterType: CampaignFilterType.Country, filterValue: 'AM' },
      ],
      10,
    )

    expect(rows).toEqual([
      { key: 10, filterType: CampaignFilterType.AllActive, filterValue: '' },
      { key: 11, filterType: CampaignFilterType.Country, filterValue: 'AM' },
    ])
  })

  it('turns rows into API segments: trimmed values, and null for a rule that takes none', () => {
    const segments = segmentsFromDrafts([
      { key: 1, filterType: CampaignFilterType.AllActive, filterValue: 'left over' },
      { key: 2, filterType: CampaignFilterType.Product, filterValue: ' 12 ' },
    ])

    expect(segments).toEqual([
      { filterType: CampaignFilterType.AllActive, filterValue: null },
      { filterType: CampaignFilterType.Product, filterValue: '12' },
    ])
  })
})

describe('segmentKey', () => {
  it('names the rule and its value, and treats a missing value as empty', () => {
    expect(segmentKey({ filterType: CampaignFilterType.Country, filterValue: 'AM' })).toBe('Country|AM')
    expect(segmentKey({ filterType: CampaignFilterType.AllActive, filterValue: null })).toBe('AllActive|')
  })
})
