/**
 * Tests of the stat row vocabulary: the two looks of a row and the figure a screen hands each tile.
 */
import { describe, expect, it } from 'vitest'
import { UiStatRowVariant } from '@/utils/ui/statRow'
import type { UiStatFigure } from '@/utils/ui/statRow'
import { UiTone } from '@/utils/ui/tone'

describe('UiStatRowVariant', () => {
  it('has the tile and the inline look', () => {
    expect(Object.values(UiStatRowVariant)).toEqual(['tile', 'inline'])
  })

  it('exposes the named members as their literal values', () => {
    expect(UiStatRowVariant.Tile).toBe('tile')
    expect(UiStatRowVariant.Inline).toBe('inline')
  })
})

describe('UiStatFigure', () => {
  it('needs only a value, as a string or a number', () => {
    const text: UiStatFigure = { value: '1,200' }
    const count: UiStatFigure = { value: 0 }
    expect(text.value).toBe('1,200')
    expect(count.value).toBe(0)
    expect(text.delta).toBeUndefined()
    expect(text.loading).toBeUndefined()
  })

  it('takes the optional caption, tones, change and loading flag', () => {
    const figure: UiStatFigure = {
      value: 42,
      label: 'This month',
      tone: UiTone.Success,
      delta: '+5%',
      deltaTone: UiTone.Danger,
      loading: true,
    }
    expect(figure).toEqual({
      value: 42,
      label: 'This month',
      tone: UiTone.Success,
      delta: '+5%',
      deltaTone: UiTone.Danger,
      loading: true,
    })
  })
})
