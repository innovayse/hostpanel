/**
 * Unit tests for the domain status maps and the types they are keyed by: every state has a
 * colour, a tone and a label colour, and the unknown state is muted rather than red.
 */
import { describe, expect, it } from 'vitest'
import { AVAILABILITY_DOT_CLASSES, AVAILABILITY_LABEL_CLASSES, AVAILABILITY_TONES } from '~/utils/domainStatus'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { DomainResultStatus } from '~/types/domainresultstatus'
import { UiTone } from '~/utils/ui/enums'

describe('domain status maps', () => {
  it.each(Object.values(DomainResultStatus))('covers the %s state in every map', (status) => {
    expect(AVAILABILITY_DOT_CLASSES[status]).toMatch(/^bg-/)
    expect(AVAILABILITY_LABEL_CLASSES[status]).toMatch(/^text-/)
    expect(AVAILABILITY_TONES[status]).toBeDefined()
  })

  it('colours available green, taken red and unknown muted', () => {
    expect(AVAILABILITY_DOT_CLASSES[DomainResultStatus.Available]).toBe('bg-ok')
    expect(AVAILABILITY_DOT_CLASSES[DomainResultStatus.Taken]).toBe('bg-danger')
    expect(AVAILABILITY_DOT_CLASSES[DomainResultStatus.Unknown]).toBe('bg-mut2')
    expect(AVAILABILITY_TONES[DomainResultStatus.Unknown]).toBe(UiTone.Default)
  })

  it('keeps the three states as the strings the stores and the markup use', () => {
    expect(Object.values(DomainResultStatus)).toEqual(['available', 'taken', 'unknown'])
  })

  it('reserves a sentinel for the unfiltered category that no registrar category can equal', () => {
    expect(ALL_CATEGORY).toBe('__all__')
  })
})
