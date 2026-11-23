/**
 * Unit tests for UiIconTile: the icon or slot text, the tones and the size steps.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiIconTile from '~/components/ui/UiIconTile.vue'
import { UiIconTileSize, UiIconTileTone } from '~/utils/ui/iconTile'

describe('UiIconTile', () => {
  it('shows the named icon and hides the decoration from assistive technology', () => {
    const wrapper = mountWithKit(UiIconTile, { props: { icon: 'mdi:server' } })

    expect(wrapper.find('[data-icon="mdi:server"]').exists()).toBe(true)
    expect(wrapper.attributes('aria-hidden')).toBe('true')
  })

  it('rests on a soft brand tint that fills in when the parent row is hovered or focused', () => {
    const classes = mountWithKit(UiIconTile, { props: { icon: 'mdi:server' } }).classes()

    expect(classes).toEqual(expect.arrayContaining([
      'bg-classic-brand-soft', 'text-classic-brand', 'group-hover:bg-classic-fill', 'group-focus-visible:bg-classic-fill',
    ]))
  })

  it.each([
    [UiIconTileTone.Solid, 'bg-classic-fill'],
    [UiIconTileTone.Inverse, 'bg-classic-on-brand/20'],
    [UiIconTileTone.Nova, 'bg-nova-surface-2'],
  ])('paints the %s tone', (tone, expected) => {
    expect(mountWithKit(UiIconTile, { props: { icon: 'mdi:server', tone } }).classes()).toContain(expected)
  })

  it.each([
    [UiIconTileSize.Sm, 'size-8'],
    [UiIconTileSize.Compact, 'size-9'],
    [UiIconTileSize.Md, 'size-10'],
    [UiIconTileSize.Lg, 'size-11'],
  ])('sizes the %s tile', (size, expected) => {
    expect(mountWithKit(UiIconTile, { props: { icon: 'mdi:server', size } }).classes()).toContain(expected)
  })

  it('rounds the large tile more than the others and sizes its icon in pixels', () => {
    const wrapper = mountWithKit(UiIconTile, { props: { icon: 'mdi:server', size: UiIconTileSize.Lg, tone: UiIconTileTone.Nova } })

    expect(wrapper.classes()).toContain('rounded-xl')
    expect(wrapper.find('[data-icon="mdi:server"]').classes()).toContain('text-[22px]')
  })

  it('shows slot text instead of an icon', () => {
    const wrapper = mountWithKit(UiIconTile, { slots: { default: 'DE' } })

    expect(wrapper.text()).toBe('DE')
    expect(wrapper.find('[data-icon]').exists()).toBe(false)
  })

  it('is a medium soft tile by default', () => {
    expect(mountWithKit(UiIconTile, { props: { icon: 'mdi:server' } }).classes()).toEqual(expect.arrayContaining(['size-10', 'rounded-lg']))
  })
})
