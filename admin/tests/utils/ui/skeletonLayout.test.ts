/**
 * Unit tests for the skeleton kit's layout vocabulary: grid columns for a count of stacks and the field bars.
 */
import { describe, expect, it } from 'vitest'
import { fieldBarClasses, fieldBarClassOf, gridColumnsOf, ownsFieldBody } from '@/utils/ui/skeletonLayout'
import { SkeletonFieldKind } from '@/utils/ui/skeletonSchema'

describe('gridColumnsOf', () => {
  it('takes one column for one stack, never a two column grid', () => {
    expect(gridColumnsOf(1)).toBe(1)
    expect(gridColumnsOf(0)).toBe(1)
  })

  it('takes a column per stack up to four', () => {
    expect(gridColumnsOf(2)).toBe(2)
    expect(gridColumnsOf(3)).toBe(3)
    expect(gridColumnsOf(4)).toBe(4)
  })

  it('stops at four for a longer list', () => {
    expect(gridColumnsOf(7)).toBe(4)
  })
})

describe('fieldBarClassOf', () => {
  it('leaves a single line input to the field skeleton, whether the kind is named or not', () => {
    expect(fieldBarClassOf({ key: 'a' })).toBeUndefined()
    expect(fieldBarClassOf({ key: 'a', kind: SkeletonFieldKind.Input })).toBeUndefined()
  })

  it('gives a textarea, a toggle and a check a bar of their own', () => {
    expect(fieldBarClassOf({ key: 'a', kind: SkeletonFieldKind.Textarea })).toBe(fieldBarClasses.textarea)
    expect(fieldBarClassOf({ key: 'a', kind: SkeletonFieldKind.Toggle })).toBe(fieldBarClasses.toggle)
    expect(fieldBarClassOf({ key: 'a', kind: SkeletonFieldKind.Check })).toBe(fieldBarClasses.check)
  })

  it('makes a textarea taller than a toggle', () => {
    expect(fieldBarClasses.textarea).toContain('h-24')
  })

  it('draws no single bar for a radio group, which draws its options', () => {
    expect(fieldBarClassOf({ key: 'a', kind: SkeletonFieldKind.Radio })).toBeUndefined()
  })

  it('shapes a toggle as a switch', () => {
    expect(fieldBarClasses.toggle).toContain('rounded-full')
  })
})

describe('ownsFieldBody', () => {
  it('is false for an input, named or not, and true for every other kind', () => {
    expect(ownsFieldBody({ key: 'a' })).toBe(false)
    expect(ownsFieldBody({ key: 'a', kind: SkeletonFieldKind.Input })).toBe(false)
    expect(ownsFieldBody({ key: 'a', kind: SkeletonFieldKind.Textarea })).toBe(true)
    expect(ownsFieldBody({ key: 'a', kind: SkeletonFieldKind.Toggle })).toBe(true)
    expect(ownsFieldBody({ key: 'a', kind: SkeletonFieldKind.Check })).toBe(true)
    expect(ownsFieldBody({ key: 'a', kind: SkeletonFieldKind.Radio })).toBe(true)
  })
})
