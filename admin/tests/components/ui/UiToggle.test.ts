/**
 * Unit tests for UiToggle: switch semantics, flipping and the disabled state.
 */
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiToggle from '@/components/ui/UiToggle.vue'

/** The stylesheet that declares the switch tokens. */
const CSS = readFileSync(join(__dirname, '..', '..', '..', 'src', 'assets', 'main.css'), 'utf8')

/** Closed set of the switch tokens declared in the stylesheet. */
const ToggleToken = { Track: 'track', Knob: 'knob', Inset: 'inset' } as const

/** One of the switch tokens. */
type ToggleToken = (typeof ToggleToken)[keyof typeof ToggleToken]

/**
 * Reads one switch token from the stylesheet, in rem.
 *
 * @param name - The token's last segment.
 * @returns The declared length in rem.
 */
const toggleToken = (name: ToggleToken): number => {
  const match = new RegExp(String.raw`--spacing-toggle-${name}:\s*([0-9.]+)rem;`).exec(CSS)
  if (match === null) {
    throw new Error(`No --spacing-toggle-${name} token in main.css`)
  }
  return Number(match[1])
}

describe('UiToggle', () => {
  it('is a switch that reports its state', () => {
    const on = mount(UiToggle, { props: { modelValue: true } })
    const off = mount(UiToggle, { props: { modelValue: false } })

    expect(on.attributes('role')).toBe('switch')
    expect(on.attributes('aria-checked')).toBe('true')
    expect(off.attributes('aria-checked')).toBe('false')
  })

  it('emits the flipped state on click', async () => {
    const wrapper = mount(UiToggle, { props: { modelValue: false } })

    await wrapper.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('does nothing while disabled', async () => {
    const wrapper = mount(UiToggle, { props: { modelValue: false, disabled: true } })

    await wrapper.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    expect(wrapper.attributes('disabled')).toBeDefined()
  })

  it('keeps data-testid on the button', () => {
    const wrapper = mount(UiToggle, { props: { modelValue: false }, attrs: { 'data-testid': 'probe' } })

    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('colours the track from the state', () => {
    expect(mount(UiToggle, { props: { modelValue: true } }).classes()).toContain('not-disabled:gradient-brand')
    expect(mount(UiToggle, { props: { modelValue: false } }).classes()).toContain('bg-control-track')
  })

  it('shows a focus ring and a muted disabled track', () => {
    const wrapper = mount(UiToggle, { props: { modelValue: true, disabled: true } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['focus-visible:ring-2', 'disabled:bg-surface-hover', 'disabled:cursor-not-allowed']))
  })

  it('derives the track height and the knob travel from the switch tokens', () => {
    const on = mount(UiToggle, { props: { modelValue: true } })
    const knob = on.get('span').classes().join(' ')

    expect(on.classes()).toEqual(expect.arrayContaining(['w-toggle-track', 'px-toggle-inset']))
    expect(on.classes()).toContain('h-[calc(var(--spacing-toggle-knob)+2*var(--spacing-toggle-inset))]')
    expect(knob).toContain('w-toggle-knob')
    expect(knob).toContain('translate-x-[calc(var(--spacing-toggle-track)-var(--spacing-toggle-knob)-2*var(--spacing-toggle-inset))]')
    expect(mount(UiToggle, { props: { modelValue: false } }).get('span').classes()).toContain('translate-x-0')
  })

  it('moves the knob by the travel its class declares, which leaves the same gap on both sides in the on state', () => {
    const track = toggleToken(ToggleToken.Track)
    const knob = toggleToken(ToggleToken.Knob)
    const inset = toggleToken(ToggleToken.Inset)
    const knobClass = mount(UiToggle, { props: { modelValue: true } }).get('span').element.className
    const formula = /translate-x-\[calc\((.+?)\)\]/.exec(knobClass)?.[1]

    expect(formula).toBeDefined()
    // The class is evaluated with the tokens read from the stylesheet, so a change of either side fails here.
    const travel = Number(Function(`return ${String(formula).replace(/var\(--spacing-toggle-(\w+)\)/g, (_, name: ToggleToken) => String(toggleToken(name)))}`)())
    expect(inset).toBeGreaterThan(0)
    expect(travel).toBeGreaterThan(0)
    expect(track - (inset + travel) - knob).toBeCloseTo(inset, 10)
  })
})
