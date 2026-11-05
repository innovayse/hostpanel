/**
 * Unit tests for UiToneCard: the coloured band follows the tone and the slot is the body.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiToneCard from '~/components/ui/UiToneCard.vue'
import { UiTone } from '~/utils/ui/enums'

describe('UiToneCard', () => {
  it.each([[UiTone.Success, 'border-l-green-500'], [UiTone.Danger, 'border-l-red-500'], [UiTone.Info, 'border-l-blue-500']])('draws the %s edge', (tone, edge) => {
    const wrapper = mountWithKit(UiToneCard, { props: { tone }, slots: { default: 'Body' } })

    expect(wrapper.classes()).toContain(edge)
    expect(wrapper.classes()).toContain('border-l-4')
    expect(wrapper.text()).toBe('Body')
  })
})
