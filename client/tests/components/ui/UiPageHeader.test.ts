/**
 * Unit tests for UiPageHeader: the h1 title, description, icon, breadcrumbs and actions slots.
 */
import { describe, expect, it } from 'vitest'
import { Plus, RefreshCw, Server } from 'lucide-vue-next'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiPageHeader from '~/components/ui/UiPageHeader.vue'
import type { UiHeaderAction } from '~/types/uiheaderaction'

describe('UiPageHeader', () => {
  it('renders the title as the page h1', () => {
    const wrapper = mountWithKit(UiPageHeader, { props: { title: 'Services' } })

    expect(wrapper.find('h1').text()).toBe('Services')
    expect(wrapper.element.tagName).toBe('HEADER')
  })

  it('renders the description and the icon when given', () => {
    const wrapper = mountWithKit(UiPageHeader, { props: { title: 'Services', description: 'Everything you host.', icon: Server } })

    expect(wrapper.text()).toContain('Everything you host.')
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('renders neither without them', () => {
    const wrapper = mountWithKit(UiPageHeader, { props: { title: 'Services' } })

    expect(wrapper.find('p').exists()).toBe(false)
    expect(wrapper.find('svg').exists()).toBe(false)
  })

  it('renders the breadcrumbs and actions slots', () => {
    const wrapper = mountWithKit(UiPageHeader, {
      props: { title: 'Services' },
      slots: { breadcrumbs: 'Home / Services', actions: 'Order new' },
    })

    expect(wrapper.text()).toContain('Home / Services')
    expect(wrapper.text()).toContain('Order new')
  })

  it('omits the empty slot wrappers', () => {
    const wrapper = mountWithKit(UiPageHeader, { props: { title: 'Services' } })

    expect(wrapper.find('[data-testid="ui-page-header-buttons"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-page-header-menu"]').exists()).toBe(false)
    expect(wrapper.html()).not.toContain('mb-4')
  })

  it('draws the back link on its own row above the title', () => {
    const wrapper = mountWithKit(UiPageHeader, { props: { title: 'New', backTo: '/client/tickets', backLabel: 'Tickets', backTestid: 'back' } })

    const back = wrapper.find('[data-testid="back"]')
    expect(back.attributes('href')).toBe('/client/tickets')
    expect(back.text()).toContain('Tickets')
    expect(wrapper.html().indexOf('data-testid="back"')).toBeLessThan(wrapper.html().indexOf('<h1'))
  })

  it('draws no back link without both the target and the label', () => {
    expect(mountWithKit(UiPageHeader, { props: { title: 'New', backTo: '/x' } }).find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
  })

  it('shrinks the title on a phone', () => {
    const heading = mountWithKit(UiPageHeader, { props: { title: 'Services' } }).find('h1')

    expect(heading.classes()).toEqual(expect.arrayContaining(['text-2xl', 'sm:text-3xl', 'break-words']))
  })

  describe('actions', () => {
    /** A create-like action and a secondary one. */
    const ACTIONS: UiHeaderAction[] = [
      { key: 'refresh', label: 'Refresh', icon: RefreshCw, onSelect: () => undefined, testid: 'hdr-refresh' },
      { key: 'new', label: 'Order new', icon: Plus, to: '/new', primary: true, testid: 'hdr-new' },
    ]

    it('lists the buttons from sm with the primary one first and filled', () => {
      const wrapper = mountWithKit(UiPageHeader, { props: { title: 'T', actions: ACTIONS } })

      const buttons = wrapper.find('[data-testid="ui-page-header-buttons"]')
      expect(buttons.classes()).toEqual(expect.arrayContaining(['hidden', 'sm:flex']))
      expect(buttons.findAll('a, button').map((el) => el.attributes('data-testid'))).toEqual(['hdr-new', 'hdr-refresh'])
      expect(buttons.find('[data-testid="hdr-new"]').classes()).toContain('ui-btn-primary')
    })

    it('folds every action into one plus button when one is primary, and opens a menu with full labels', async () => {
      const wrapper = mountWithKit(UiPageHeader, { props: { title: 'T', actions: ACTIONS } })

      const trigger = wrapper.find('[data-testid="ui-page-header-menu"]')
      expect(trigger.attributes('aria-label')).toBe('ui.pageHeader.actions')
      expect(trigger.classes()).toContain('bg-primary-600')
      expect(wrapper.find('[data-testid="hdr-new-menu"]').exists()).toBe(false)

      await trigger.trigger('click')

      const panel = wrapper.find('[data-testid="ui-popover-panel"]')
      expect(panel.attributes('role')).toBe('menu')
      const rows = panel.findAll('[role="menuitem"]')
      expect(rows.map((row) => row.text())).toEqual(['Order new', 'Refresh'])
    })

    it('uses the overflow button when no action is primary', () => {
      const wrapper = mountWithKit(UiPageHeader, { props: { title: 'T', actions: [ACTIONS[0]!] } })

      expect(wrapper.find('[data-testid="ui-page-header-menu"]').classes()).not.toContain('bg-primary-600')
    })

    it('runs a plain action from its button and from its menu row, and closes the menu', async () => {
      let runs = 0
      const action: UiHeaderAction = { key: 'refresh', label: 'Refresh', icon: RefreshCw, onSelect: () => { runs += 1 }, testid: 'hdr-refresh' }
      const wrapper = mountWithKit(UiPageHeader, { props: { title: 'T', actions: [action] } })

      await wrapper.find('[data-testid="hdr-refresh"]').trigger('click')
      await wrapper.find('[data-testid="ui-page-header-menu"]').trigger('click')
      await wrapper.find('[data-testid="hdr-refresh-menu"]').trigger('click')

      expect(runs).toBe(2)
      expect(wrapper.find('[data-testid="hdr-refresh-menu"]').exists()).toBe(false)
    })

    it('keeps the actions slot at every width', () => {
      const wrapper = mountWithKit(UiPageHeader, { props: { title: 'T' }, slots: { actions: 'Plugin block' } })

      expect(wrapper.text()).toContain('Plugin block')
    })
  })
})
