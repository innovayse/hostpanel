/**
 * Smoke tests for SlideFormView: create and edit modes, the locale tabs, the feature list, the
 * image upload, saving through the store at network level, and the missing-slide message. The
 * icon and date-picker libraries are replaced by stubs.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import SlideFormView from '@/views/SlideFormView.vue'
import schema from '@/views/SlideFormView.schema'
import type { Slide } from '@/types/slide'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import { toastMessages } from '../helpers/toasts'

vi.mock('@iconify/vue', async () => {
  const { defineComponent, h } = await import('vue')
  return { Icon: defineComponent({ name: 'IconStub', props: ['icon'], setup: (props) => () => h('i', { 'data-icon': props.icon }) }) }
})

vi.mock('@vuepic/vue-datepicker', async () => {
  const { defineComponent, h } = await import('vue')
  return { VueDatePicker: defineComponent({ name: 'VueDatePicker', props: ['modelValue'], setup: () => () => h('div') }) }
})

/** A stored slide with English and Russian text. */
const stored: Slide = {
  id: 3,
  iconName: 'star',
  brandColor: '#112233',
  imageUrl: '/img/s.png',
  demoUrl: null,
  learnMoreUrl: null,
  productId: null,
  productName: null,
  sortOrder: 2,
  isActive: true,
  targetAudience: 'Guest',
  visibleFrom: null,
  visibleUntil: null,
  createdAt: '2026-01-01T00:00:00Z',
  translations: [
    { locale: 'en', title: 'Hello', tagline: null, description: null, features: ['Fast'], ctaText: null, ctaUrl: null },
    { locale: 'ru', title: 'Привет', tagline: null, description: null, features: null, ctaText: null, ctaUrl: null },
  ],
}

/** Answers for what the form loads. */
const routes = {
  'GET /products': () => json([{ id: 4, name: 'Plan', slug: 'plan' }]),
  'GET /admin/slides': () => json([stored]),
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SlideFormView', () => {
  it('opens an empty create form with the trail and the create button', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/create', '/settings/slides/create')

    expect(wrapper.find('[data-testid="ui-breadcrumb"]').exists()).toBe(false)
    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.get('h1').text()).toBe('Create slide')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create slide')
    expect((wrapper.get('[data-testid="slide-form-title"]').element as HTMLInputElement).value).toBe('')
  })

  it('saves a new slide with only the translations that have a title, then returns to the list', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /admin/slides': () => json(9) })
    const { wrapper, router } = await mountScreen(SlideFormView, {}, '/settings/slides/create', '/settings/slides/create')

    await wrapper.get('[data-testid="slide-form-title"]').setValue('  Launch  ')
    await wrapper.get('[data-testid="slide-form-feature-add"]').trigger('click')
    await wrapper.get('[data-testid="slide-form-feature-0"]').setValue('Quick')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/settings/slides')
    })

    const post = mock.mock.calls.find((call) => (call[1] as RequestInit | undefined)?.method === 'POST')
    const body = JSON.parse((post?.[1] as RequestInit | undefined)?.body as string) as { translations: { locale: string; title: string; features: string[] | null }[] }
    expect(body.translations).toEqual([expect.objectContaining({ locale: 'en', title: 'Launch', features: ['Quick'] })])
  })

  it('keeps each locale tab separate', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/create', '/settings/slides/create')

    await wrapper.get('[data-testid="slide-form-title"]').setValue('English title')
    await wrapper.get('[data-testid="slide-form-tab-ru"]').trigger('click')
    expect((wrapper.get('[data-testid="slide-form-title"]').element as HTMLInputElement).value).toBe('')

    await wrapper.get('[data-testid="slide-form-tab-en"]').trigger('click')
    expect((wrapper.get('[data-testid="slide-form-title"]').element as HTMLInputElement).value).toBe('English title')
  })

  it('loads the slide being edited and updates it with PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /admin/slides/3': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/3/edit', '/settings/slides/:id/edit')

    expect((wrapper.get('[data-testid="slide-form-title"]').element as HTMLInputElement).value).toBe('Hello')
    expect((wrapper.get('[data-testid="slide-form-icon-name"]').element as HTMLInputElement).value).toBe('star')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Save')
    await wrapper.get('[data-testid="slide-form-tab-ru"]').trigger('click')
    expect((wrapper.get('[data-testid="slide-form-title"]').element as HTMLInputElement).value).toBe('Привет')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/slides/3')
    })
  })

  it('says when the slide being edited does not exist', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/99/edit', '/settings/slides/:id/edit')

    expect(wrapper.get('[data-testid="slide-form-load-error"]').text()).toBe('Slide #99 not found.')
    expect(wrapper.find('[data-testid="slide-form-title"]').exists()).toBe(false)
  })

  it('offers a retry when the slide cannot be loaded', async () => {
    let refused = true
    stubRouteTable({
      ...routes,
      'GET /admin/slides': () => (refused ? problem('Not allowed.', 403) : json([stored])),
    })
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/3/edit', '/settings/slides/:id/edit')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="slide-form-load-error"]').text()).toBe('Not allowed.')
    })
    refused = false
    await wrapper.get('[data-testid="slide-form-load-error-retry"]').trigger('click')

    await vi.waitFor(() => {
      expect((wrapper.get('[data-testid="slide-form-title"]').element as HTMLInputElement).value).toBe('Hello')
    })
  })

  it('toasts the API sentence when saving is refused and stays on the form', async () => {
    stubRouteTable({ ...routes, 'POST /admin/slides': () => problem('A slide needs an image.', 400) })
    const { wrapper, router } = await mountScreen(SlideFormView, {}, '/settings/slides/create', '/settings/slides/create')

    await wrapper.get('[data-testid="slide-form-title"]').setValue('X')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(toastMessages()).toEqual(['A slide needs an image.'])
    })
    expect(router.currentRoute.value.path).toBe('/settings/slides/create')
  })

  it('shows the refused icon and image under their own fields', async () => {
    stubRouteTable({
      ...routes,
      'POST /admin/slides': () => json({ type: 'VALIDATION_ERROR', title: 'Validation failed', status: 400, detail: 'One or more validation errors occurred.', errors: { iconName: ['Pick an icon.'], imageUrl: ['Add an image.'] } }, 400),
    })
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/create', '/settings/slides/create')

    await wrapper.get('[data-testid="slide-form-title"]').setValue('X')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Pick an icon.')
    })
    expect(wrapper.text()).toContain('Add an image.')
  })

  it('stages the uploaded image URL and previews it', async () => {
    stubRouteTable({ ...routes, 'POST /admin/storefront/upload-image': () => json({ url: '/img/up.png' }) })
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/create', '/settings/slides/create')
    const input = wrapper.get('[data-testid="slide-form-image-file"]')
    Object.defineProperty(input.element, 'files', { value: [new File(['x'], 'a.png')], configurable: true })

    await input.trigger('change')

    await vi.waitFor(() => {
      expect((wrapper.get('[data-testid="slide-form-image-url"]').element as HTMLInputElement).value).toBe('/img/up.png')
    })
    expect(wrapper.find('[data-testid="slide-form-image-preview"]').exists()).toBe(true)
  })

  it('removes a feature line', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/3/edit', '/settings/slides/:id/edit')
    expect(wrapper.find('[data-testid="slide-form-feature-0"]').exists()).toBe(true)

    await wrapper.get('[data-testid="slide-form-feature-remove-0"]').trigger('click')

    expect(wrapper.find('[data-testid="slide-form-feature-0"]').exists()).toBe(false)
  })

  it('draws every section and field of its schema', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/create', '/settings/slides/create')

    for (const section of schema.columns.flat()) {
      expect(wrapper.find(`[data-testid="ui-form-section-${section.key}"]`).exists()).toBe(true)
      for (const field of section.fields) {
        expect(wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()).toBe(true)
      }
    }
  })

  it('keeps the fields of its schema as placeholders while the slide loads', async () => {
    stubRouteTable({ ...routes, 'GET /admin/slides': () => new Promise<Response>(() => undefined) })
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/3/edit', '/settings/slides/:id/edit')

    await vi.waitFor(() => {
      expect(wrapper.findAll('[data-testid="ui-form-section-general"][aria-busy="true"]')).toHaveLength(1)
    })
    expect(wrapper.find('[data-testid="slide-form-title"]').exists()).toBe(false)
  })

  it('marks the language tabs with no text and drops the mark of a language once it is written', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(SlideFormView, {}, '/settings/slides/create', '/settings/slides/create')

    for (const locale of ['en', 'ru', 'hy']) {
      expect(wrapper.find(`[data-testid="slide-form-tab-${locale}-empty"]`).exists()).toBe(true)
    }

    await wrapper.get('[data-testid="slide-form-title"]').setValue('Written')

    expect(wrapper.find('[data-testid="slide-form-tab-en-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="slide-form-tab-ru-empty"]').exists()).toBe(true)
  })

  it('lets the reader leave a loaded slide without a question when nothing was edited', async () => {
    // A slide linked to a product makes the form fill its learn more link while it loads, which must not count as an edit.
    stubRouteTable({ ...routes, 'GET /admin/slides': () => json([{ ...stored, productId: 4, productName: 'Plan' }]) })
    const { wrapper, router } = await mountScreen(SlideFormView, {}, '/settings/slides/3/edit', '/settings/slides/:id/edit')

    await router.push('/settings/slides')

    expect(router.currentRoute.value.path).toBe('/settings/slides')
    expect(wrapper.find('[data-testid="dialog-panel"]').exists()).toBe(false)
  })

  it('asks before leaving a loaded slide that was edited', async () => {
    stubRouteTable(routes)
    const { wrapper, router } = await mountScreen(SlideFormView, {}, '/settings/slides/3/edit', '/settings/slides/:id/edit')
    await wrapper.get('[data-testid="slide-form-title"]').setValue('Changed')

    void router.push('/settings/slides')

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="dialog-panel"]').exists()).toBe(true)
    })
    expect(router.currentRoute.value.path).toBe('/settings/slides/3/edit')
  })
})
