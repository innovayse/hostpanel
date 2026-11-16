/**
 * Tests for the published-state filter helper: the choice a list screen reads from its address becomes the API's switch.
 */
import { describe, expect, it } from 'vitest'
import { isPublishedOfFilter } from '@/lib/publishedFilter'
import { PublishedFilter } from '@/types/publishedfilter'

describe('isPublishedOfFilter', () => {
  it('turns the published choice into true', () => {
    expect(isPublishedOfFilter(PublishedFilter.Published)).toBe(true)
  })

  it('turns the draft choice into false', () => {
    expect(isPublishedOfFilter(PublishedFilter.Draft)).toBe(false)
  })

  it('leaves the list unnarrowed when no choice is set', () => {
    expect(isPublishedOfFilter(undefined)).toBeUndefined()
  })
})
