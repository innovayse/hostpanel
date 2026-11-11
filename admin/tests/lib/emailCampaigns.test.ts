/**
 * Unit tests for the email campaign rules: what each audience filter accepts as a value, whether a
 * set of rows is complete, and the round trip between form rows and API segments.
 */
import { describe, expect, it } from 'vitest'
import {
  areSegmentsComplete,
  CAMPAIGN_FILTER_TYPES,
  draftsFromSegments,
  filterTakesValue,
  isFilterValueValid,
  newSegmentDraft,
  segmentsFromDrafts,
} from '@/lib/emailCampaigns'
import { CampaignFilterType } from '@/types/emailcampaign'

describe('CAMPAIGN_FILTER_TYPES', () => {
  it('offers every filter type once', () => {
    expect([...CAMPAIGN_FILTER_TYPES].sort()).toEqual(Object.values(CampaignFilterType).sort())
  })
})

describe('filterTakesValue', () => {
  it('is false only for all active clients', () => {
    expect(filterTakesValue(CampaignFilterType.AllActive)).toBe(false)
    expect(filterTakesValue(CampaignFilterType.Product)).toBe(true)
    expect(filterTakesValue(CampaignFilterType.Country)).toBe(true)
    expect(filterTakesValue(CampaignFilterType.ServiceExpiringInDays)).toBe(true)
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
