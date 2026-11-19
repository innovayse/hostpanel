/**
 * Unit tests for UiPageHeader: title as the page heading, subtitle and slots.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { config, mount, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import { Icon } from '@/lib/icons'
import { usePageTitleStore } from '@/stores/pageTitle'
import type { UiHeaderAction } from '@/utils/ui/actionBar'
import { UiButtonVariant } from '@/utils/ui/button'
import { createTestI18n } from '../../helpers/i18n'

config.global.plugins = [createTestI18n()]

/** Two actions: a link in the primary look and a secondary button, each with its icon. */
const ACTIONS: UiHeaderAction[] = [
  { label: 'Refresh', icon: Icon.Refresh, variant: UiButtonVariant.Secondary, testid: 'h-refresh', onSelect: () => undefined },
  { label: 'Add', icon: Icon.Add, variant: UiButtonVariant.Primary, testid: 'h-add', to: '/add' },
]

describe('UiPageHeader', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('renders the title as the level-1 heading', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients' } })

    expect(wrapper.get('h1').text()).toBe('Clients')
  })

  it('renders the subtitle when given', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients', subtitle: 'All accounts' } })

    expect(wrapper.text()).toContain('All accounts')
  })

  it('renders the actions slot and the default slot', () => {
    const wrapper = mount(UiPageHeader, {
      props: { title: 'Clients' },
      slots: { default: '<span data-testid="extra" />', actions: '<button data-testid="act" />' },
    })

    expect(wrapper.find('[data-testid="extra"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="act"]').exists()).toBe(true)
  })

  it('sets the title in the muted subtitle colour below it as the concept does', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients', subtitle: 'All accounts' } })

    expect(wrapper.get('h1').classes()).toEqual(expect.arrayContaining(['text-[1.875rem]', 'font-semibold']))
    expect(wrapper.get('p').classes()).toEqual(expect.arrayContaining(['text-text-muted', 'mt-1.5']))
  })

  it('keeps the actions top right from md up and the subtitle to a readable width, so a long subtitle never pushes them down', () => {
    const wrapper = mount(UiPageHeader, {
      props: { title: 'Clients', subtitle: 'A very long subtitle '.repeat(20) },
      slots: { actions: '<button data-testid="act" />' },
    })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex-wrap', 'md:flex-nowrap']))
    const text = wrapper.get('h1').element.closest('header > div')
    expect(text?.className).toEqual(expect.stringContaining('md:basis-0'))
    expect(text?.className).toEqual(expect.stringContaining('min-w-0'))
    expect(wrapper.get('p').classes()).toContain('max-w-[70ch]')
    expect(wrapper.get('[data-testid="act"]').element.parentElement?.className).toEqual(expect.stringContaining('md:shrink-0'))
  })

  it('draws a title bar instead of the text while loading, and a subtitle bar only when one is passed', () => {
    const plain = mount(UiPageHeader, { props: { title: 'Clients', loading: true } })
    const withSubtitle = mount(UiPageHeader, { props: { title: 'Clients', subtitle: 'All accounts', loading: true } })

    expect(plain.find('h1').exists()).toBe(false)
    expect(plain.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(1)
    expect(withSubtitle.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(2)
    expect(withSubtitle.text()).not.toContain('All accounts')
    expect(plain.attributes('aria-busy')).toBe('true')
  })

  it('draws the title and a bar for the subtitle alone while only the subtitle loads', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients', subtitle: 'All accounts', subtitleLoading: true } })

    expect(wrapper.get('h1').text()).toBe('Clients')
    expect(wrapper.find('[data-testid="ui-page-header-subtitle-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-page-header-skeleton"]').exists()).toBe(false)
    expect(wrapper.find('p').exists()).toBe(false)
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })

  it('draws the subtitle and no bar when the subtitle is not loading', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients', subtitle: 'All accounts' } })

    expect(wrapper.find('[data-testid="ui-page-header-subtitle-skeleton"]').exists()).toBe(false)
    expect(wrapper.get('p').text()).toBe('All accounts')
    expect(wrapper.attributes('aria-busy')).toBeUndefined()
  })

  it('draws a subtitle bar while the title loads when the subtitle is made of data too', () => {
    const wrapper = mount(UiPageHeader, { props: { loading: true, subtitleLoading: true } })

    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(2)
  })

  it('needs no title while loading', () => {
    const wrapper = mount(UiPageHeader, { props: { loading: true } })

    expect(wrapper.find('h1').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(1)
  })

  it('keeps the actions while loading', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients', loading: true }, slots: { actions: '<button data-testid="act" />' } })

    expect(wrapper.find('[data-testid="act"]').exists()).toBe(true)
  })

  it('keeps the actions on the right of the title row, wrapping only below sm so they never leave the viewport', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients' }, slots: { actions: '<button data-testid="act" />' } })
    const actions = wrapper.get('[data-testid="act"]').element.parentElement

    expect(actions?.classList.contains('flex-wrap')).toBe(true)
    expect(actions?.classList.contains('sm:flex-nowrap')).toBe(true)
    expect(actions?.classList.contains('whitespace-nowrap')).toBe(true)
  })

  it('keeps 22px under itself less the gap its stack already adds', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients' } })

    expect(wrapper.classes()).toContain('mb-[calc(1.375rem-var(--stack-gap,0rem))]')
  })

  it('draws the back pill left of the title when it has a target and a label', () => {
    const wrapper = mount(UiPageHeader, {
      props: { title: 'Plugin', backTo: '/plugins', backLabel: 'Plugins', backTestid: 'probe-back' },
      global: { stubs: { RouterLink: RouterLinkStub } },
    })
    const back = wrapper.get('[data-testid="probe-back"]')

    expect(wrapper.findComponent(RouterLinkStub).props('to')).toBe('/plugins')
    expect(back.text()).toBe('Plugins')
    expect(back.classes()).toContain('rounded-pill')
    // The back pill comes before the heading in the document, so it sits to its left.
    expect(back.element.compareDocumentPosition(wrapper.get('h1').element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('draws no back link without a target, or without a label', () => {
    expect(mount(UiPageHeader, { props: { title: 'Plugin' } }).find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
    const noLabel = mount(UiPageHeader, { props: { title: 'Plugin', backTo: '/plugins' }, global: { stubs: { RouterLink: RouterLinkStub } } })
    const noTarget = mount(UiPageHeader, { props: { title: 'Plugin', backLabel: 'Plugins' }, global: { stubs: { RouterLink: RouterLinkStub } } })

    expect(noLabel.find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
    expect(noTarget.find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
  })

  it('names the back link ui-page-header-back unless the screen gives its own test id', () => {
    const wrapper = mount(UiPageHeader, {
      props: { title: 'Plugin', backTo: '/plugins', backLabel: 'Plugins' },
      global: { stubs: { RouterLink: RouterLinkStub } },
    })

    expect(wrapper.find('[data-testid="ui-page-header-back"]').exists()).toBe(true)
  })

  it('hides the back pill from md up when the top bar breadcrumb covers its target', () => {
    const wrapper = mount(UiPageHeader, {
      props: { title: 'Plugin', backTo: '/plugins', backLabel: 'Plugins', backMobileOnly: true },
      global: { stubs: { RouterLink: RouterLinkStub } },
    })

    expect(wrapper.get('[data-testid="ui-page-header-back"]').classes()).toContain('md:hidden')
  })

  it('draws the back pill with its label and no link target while the screen is a skeleton', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Plugin', backLabel: 'Plugins', backPending: true } })
    const back = wrapper.get('[data-testid="ui-page-header-back"]')

    expect(back.text()).toBe('Plugins')
    expect(back.attributes('href')).toBeUndefined()
  })

  describe('page title', () => {
    it('announces its title to the page title store and follows a change of it', async () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Clients' } })
      expect(usePageTitleStore().title).toBe('Clients')

      await wrapper.setProps({ title: 'Ada Lovelace' })

      expect(usePageTitleStore().title).toBe('Ada Lovelace')
    })

    it('announces nothing while the title is loading, and the title once it has loaded', async () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Ada Lovelace', loading: true } })
      expect(usePageTitleStore().title).toBeUndefined()

      await wrapper.setProps({ loading: false })

      expect(usePageTitleStore().title).toBe('Ada Lovelace')
    })

    it('withdraws its title when it goes', () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Clients' } })

      wrapper.unmount()

      expect(usePageTitleStore().title).toBeUndefined()
    })
  })

  describe('actions', () => {
    it('draws each action as a button or a link with its icon and label', () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Clients', actions: ACTIONS }, global: { stubs: { RouterLink: RouterLinkStub } } })

      expect(wrapper.get('[data-testid="h-refresh"]').element.tagName).toBe('BUTTON')
      expect(wrapper.get('[data-testid="h-refresh"]').find('svg').exists()).toBe(true)
      expect(wrapper.get('[data-testid="h-refresh"]').text()).toBe('Refresh')
      expect(wrapper.get('[data-testid="h-add"]').find('svg').exists()).toBe(true)
      expect(wrapper.findComponent(RouterLinkStub).props('to')).toBe('/add')
      expect(wrapper.find('[data-testid="ui-page-header-more"]').exists()).toBe(false)
    })

    it('runs the action when its button is pressed, unless it is disabled', async () => {
      let ran = 0
      const wrapper = mount(UiPageHeader, {
        props: { title: 'Clients', actions: [{ ...ACTIONS[0]!, onSelect: () => { ran += 1 } }, { ...ACTIONS[1]!, to: undefined, disabled: true, onSelect: () => { ran += 10 } }] },
      })

      await wrapper.get('[data-testid="h-refresh"]').trigger('click')
      await wrapper.get('[data-testid="h-add"]').trigger('click')

      expect(ran).toBe(1)
    })

    it('draws the slot before the actions', () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Clients', actions: ACTIONS }, slots: { actions: '<i data-testid="help" />' }, global: { stubs: { RouterLink: RouterLinkStub } } })
      const order = wrapper.findAll('[data-testid="help"], [data-testid="h-refresh"]').map((node) => node.attributes('data-testid'))

      expect(order).toEqual(['help', 'h-refresh'])
    })

    it('keeps only the primary action as a button on a phone and folds the others into one menu', () => {
      const original = window.matchMedia
      window.matchMedia = ((query: string) => ({ matches: true, media: query, addEventListener: () => undefined, removeEventListener: () => undefined })) as unknown as typeof window.matchMedia
      try {
        const wrapper = mount(UiPageHeader, { props: { title: 'Clients', actions: ACTIONS }, global: { stubs: { RouterLink: RouterLinkStub } } })

        expect(wrapper.find('[data-testid="h-add"]').exists()).toBe(true)
        expect(wrapper.find('[data-testid="h-refresh"]').exists()).toBe(false)
        expect(wrapper.find('[data-testid="ui-page-header-more"]').exists()).toBe(true)
      } finally {
        window.matchMedia = original
      }
    })
  })
})
