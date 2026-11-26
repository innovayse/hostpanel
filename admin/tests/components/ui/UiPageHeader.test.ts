/**
 * Unit tests for UiPageHeader: title as the page heading, subtitle and slots, the breadcrumb trail above the title, the
 * badge beside it and the actions on its line.
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

    const text = wrapper.get('h1').element.parentElement?.parentElement
    expect(text?.parentElement?.classList.contains('flex-wrap')).toBe(true)
    expect(text?.className).toEqual(expect.stringContaining('min-w-0'))
    expect(wrapper.get('p').classes()).toContain('max-w-[70ch]')
  })

  it('stacks the header in a column and wraps its title row, so the actions drop under a long title instead of squeezing it', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Service #1 - A long product name' }, slots: { actions: '<button data-testid="act" />' } })
    const text = wrapper.get('h1').element.parentElement?.parentElement
    const actions = wrapper.get('[data-testid="act"]').element.parentElement

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex', 'flex-col']))
    expect(wrapper.classes()).not.toContain('flex-wrap')
    expect(text?.parentElement?.classList.contains('flex-wrap')).toBe(true)
    // The text column grows from a 20rem basis and may shrink, so the title wraps by words and never per letter.
    expect(text?.classList.contains('flex-[1_1_20rem]')).toBe(true)
    expect(text?.classList.contains('min-w-0')).toBe(true)
    expect(wrapper.get('h1').classes()).toContain('break-words')
    expect(text?.className).not.toContain('break-all')
    // The actions row wraps too and never exceeds the header.
    expect(actions?.classList.contains('flex-wrap')).toBe(true)
    expect(actions?.classList.contains('max-w-full')).toBe(true)
    expect(actions?.classList.contains('whitespace-nowrap')).toBe(false)
    expect(actions?.classList.contains('shrink-0')).toBe(false)
  })

  it('keeps default-slot content (a switcher, a badge) inside the text column under the title', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Service' }, slots: { default: '<span data-testid="extra" />' } })

    expect(wrapper.get('[data-testid="extra"]').element.parentElement).toBe(wrapper.get('h1').element.parentElement?.parentElement)
  })

  it('draws the badge slot inline beside the title, in the title row and not under it', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Domain' }, slots: { badge: '<span data-testid="status" />', default: '<span data-testid="extra" />' } })
    const row = wrapper.get('[data-testid="ui-page-header-title-row"]')

    expect(wrapper.get('[data-testid="status"]').element.parentElement).toBe(row.element)
    expect(wrapper.get('h1').element.parentElement).toBe(row.element)
    expect(row.classes()).toEqual(expect.arrayContaining(['flex', 'items-center']))
    expect(wrapper.get('[data-testid="extra"]').element.parentElement).not.toBe(row.element)
  })

  it('draws no badge while the title loads', () => {
    const wrapper = mount(UiPageHeader, { props: { loading: true }, slots: { badge: '<span data-testid="status" />' } })

    expect(wrapper.find('[data-testid="status"]').exists()).toBe(false)
  })

  it('gives the title row and the actions one minimum height and centres both, so the buttons sit on the title line', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients' }, slots: { actions: '<button data-testid="act" />' } })
    const row = wrapper.get('[data-testid="ui-page-header-title-row"]')
    const actions = wrapper.get('[data-testid="act"]').element.parentElement

    expect(row.classes()).toEqual(expect.arrayContaining(['min-h-10.5', 'items-center']))
    expect(actions?.classList.contains('min-h-10.5')).toBe(true)
    expect(actions?.classList.contains('items-center')).toBe(true)
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

  it('keeps 22px under itself less the gap its stack already adds', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients' } })

    expect(wrapper.classes()).toContain('mb-[calc(1.375rem-var(--stack-gap,0rem))]')
  })

  describe('breadcrumb trail', () => {
    /** The whole trail of a domain of a client, the last step being the page. */
    const CRUMBS = [
      { label: 'Clients', to: '/clients' },
      { label: 'Ada Lovelace', to: '/clients/7' },
      { label: 'Domains', to: '/clients/7/domains' },
      { label: 'example.com' },
    ]

    it('draws the trail above the title, every step a link but the page itself', () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Domain example.com', crumbs: CRUMBS }, global: { stubs: { RouterLink: RouterLinkStub } } })
      const trail = wrapper.get('[data-testid="ui-breadcrumb"]')

      expect(wrapper.findAllComponents(RouterLinkStub).map((link) => link.props('to'))).toEqual(['/clients', '/clients/7', '/clients/7/domains'])
      expect(wrapper.get('[data-testid="ui-page-header-crumb-current"]').text()).toBe('example.com')
      expect(wrapper.get('[data-testid="ui-page-header-crumb-current"]').element.tagName).not.toBe('A')
      expect(trail.element.compareDocumentPosition(wrapper.get('h1').element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
      expect(trail.element.parentElement).toBe(wrapper.element)
    })

    it('draws the whole trail at every width, with a separator icon between the steps', () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Domain', crumbs: CRUMBS }, global: { stubs: { RouterLink: RouterLinkStub } } })

      expect(wrapper.findAll('[data-testid="ui-breadcrumb"] li').flatMap((item) => item.classes())).not.toContain('max-md:hidden')
      expect(wrapper.findAll('[data-testid="ui-breadcrumb"] li svg')).toHaveLength(3)
    })

    it('cuts a long name of the trail with an ellipsis', () => {
      const name = 'A client with a name too long for the line it sits on'
      const wrapper = mount(UiPageHeader, {
        props: { title: 'Domain', crumbs: [{ label: 'Clients', to: '/clients' }, { label: name, to: '/clients/7' }, { label: 'example.com' }] },
        global: { stubs: { RouterLink: RouterLinkStub } },
      })

      expect(wrapper.findAllComponents(RouterLinkStub)[1]!.classes()).toContain('truncate')
      expect(wrapper.findAllComponents(RouterLinkStub)[1]!.attributes('title')).toBe(name)
    })

    it('draws no trail without crumbs, and no back link at all', () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Plugin' } })

      expect(wrapper.find('[data-testid="ui-breadcrumb"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-page-header-crumbs-skeleton"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
    })

    it('draws a bar where the trail will be while the screen is a skeleton', () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Plugin', crumbsPending: true } })

      expect(wrapper.find('[data-testid="ui-page-header-crumbs-skeleton"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="ui-breadcrumb"]').exists()).toBe(false)
    })

    it('draws a bar in place of the last step while the page is being read', () => {
      const wrapper = mount(UiPageHeader, {
        props: { loading: true, crumbs: [{ label: 'Clients', to: '/clients' }, { label: '', pending: true }] },
        global: { stubs: { RouterLink: RouterLinkStub } },
      })

      expect(wrapper.find('[data-testid="ui-page-header-crumb-current-pending"]').exists()).toBe(true)
    })

    it('owns the trail: the page title store tells the top bar to leave its own out, and takes it back when the header goes', async () => {
      const store = usePageTitleStore()
      const wrapper = mount(UiPageHeader, { props: { title: 'Domain', crumbs: CRUMBS }, global: { stubs: { RouterLink: RouterLinkStub } } })
      expect(store.ownsTrail).toBe(true)

      await wrapper.setProps({ crumbs: undefined })
      expect(store.ownsTrail).toBe(false)

      await wrapper.setProps({ crumbsPending: true })
      expect(store.ownsTrail).toBe(true)

      wrapper.unmount()
      expect(store.ownsTrail).toBe(false)
    })

    it('does not own the trail without crumbs', () => {
      mount(UiPageHeader, { props: { title: 'Clients' } })

      expect(usePageTitleStore().ownsTrail).toBe(false)
    })
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

    it('draws the plugin icon on an action that runs through a plugin, named after the plugin, in place of the generic icon', () => {
      const action: UiHeaderAction = { ...ACTIONS[0]!, pluginIcon: { name: 'Acme Panel', iconUrl: '/api/admin/plugins/acme/icon' } }
      const wrapper = mount(UiPageHeader, { props: { title: 'Clients', actions: [action] } })

      const button = wrapper.get('[data-testid="h-refresh"]')
      expect(button.get('[data-testid="plugin-avatar-image"]').attributes('src')).toBe('/api/admin/plugins/acme/icon')
      expect(button.get('[data-testid="plugin-avatar-image"]').attributes('alt')).toBe('Acme Panel')
      expect(button.find('svg').exists()).toBe(false)
      expect(button.text()).toBe('Refresh')
    })

    it('keeps the generic icon on an action with no plugin', () => {
      const wrapper = mount(UiPageHeader, { props: { title: 'Clients', actions: [ACTIONS[0]!] } })

      expect(wrapper.find('[data-testid="plugin-avatar"]').exists()).toBe(false)
      expect(wrapper.get('[data-testid="h-refresh"]').find('svg').exists()).toBe(true)
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

    it('collapses every action into one compact plus trigger below md, the primary action first with its full label', async () => {
      const original = window.matchMedia
      window.matchMedia = ((query: string) => ({ matches: true, media: query, addEventListener: () => undefined, removeEventListener: () => undefined })) as unknown as typeof window.matchMedia
      try {
        const wrapper = mount(UiPageHeader, { props: { title: 'Clients', actions: ACTIONS }, slots: { default: '<i data-testid="filters" />' }, global: { stubs: { RouterLink: RouterLinkStub } }, attachTo: document.body })
        const trigger = wrapper.get('[data-testid="ui-page-header-more"]')

        expect(wrapper.find('[data-testid="h-add"]').exists()).toBe(false)
        expect(wrapper.find('[data-testid="h-refresh"]').exists()).toBe(false)
        expect(trigger.attributes('aria-label')).toBe('Actions')
        expect(wrapper.get('[data-testid="ui-page-header-trigger"]').classes()).toEqual(expect.arrayContaining(['size-11', 'gradient-brand']))
        expect(wrapper.find('[data-testid="filters"]').exists()).toBe(true)

        await trigger.trigger('click')
        const items = Array.from(document.body.querySelectorAll('[role^="menuitem"]')).map((node) => node.getAttribute('data-testid'))

        expect(items).toEqual(['h-add', 'h-refresh'])
        wrapper.unmount()
      } finally {
        window.matchMedia = original
      }
    })

    it('uses the overflow icon trigger below md when no action is primary', () => {
      const original = window.matchMedia
      window.matchMedia = ((query: string) => ({ matches: true, media: query, addEventListener: () => undefined, removeEventListener: () => undefined })) as unknown as typeof window.matchMedia
      try {
        const wrapper = mount(UiPageHeader, { props: { title: 'Clients', actions: [ACTIONS[0]!] } })

        expect(wrapper.get('[data-testid="ui-page-header-more"]').attributes('aria-label')).toBe('More actions')
        expect(wrapper.get('[data-testid="ui-page-header-trigger"]').classes()).not.toContain('gradient-brand')
      } finally {
        window.matchMedia = original
      }
    })
  })
})
