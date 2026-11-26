/**
 * Tests for BrandingCard: a picked file goes through the settings store (network level), its URL
 * is staged as a draft, Save emits it, and a refusal shows the API's own sentence. Every slot (light logo,
 * dark logo, mark, favicon) keeps to its own setting key, upload endpoint, preview surface and error.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import BrandingCard from '@/components/settings/BrandingCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { json, problem } from '../../helpers/fetch'
import { mountScreen } from '../../helpers/mount'
import { requestsOf, stubRouteTable } from '../../helpers/routes'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** The four branding rows, as the API seeds them. */
const rows: Setting[] = [
  { id: 11, key: 'portal.logo', value: '' },
  { id: 12, key: 'portal.logo.dark', value: '/uploads/dark.png' },
  { id: 13, key: 'portal.logo.mark', value: '' },
  { id: 14, key: 'portal.favicon', value: '' },
]

/** A file standing in for the picked image. */
const picked = new File(['x'], 'logo.png', { type: 'image/png' })

/**
 * Attaches a file to a hidden input and fires its change event, as the browser does.
 *
 * @param input - The input element.
 */
const choose = async (input: { element: Element; trigger: (event: string) => Promise<void> }): Promise<void> => {
  Object.defineProperty(input.element, 'files', { value: [picked], configurable: true })
  await input.trigger('change')
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('BrandingCard', () => {
  it('previews a stored image and offers no Save until the draft differs', async () => {
    stubRouteTable({})
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    expect(wrapper.find('[data-testid="branding-preview-logodark"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="branding-preview-logo"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="branding-save-logodark"]').exists()).toBe(false)
  })

  it('uploads the picked file, stages the returned URL and emits it on Save', async () => {
    const mock = stubRouteTable({ 'POST /admin/settings/branding/logo': () => json({ url: '/uploads/new.png' }) })
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    await choose(wrapper.get('[data-testid="branding-file-logo"]'))
    await vi.waitFor(() => {
      expect((wrapper.get('[data-testid="branding-url-logo"]').element as HTMLInputElement).value).toBe('/uploads/new.png')
    })
    expect(requestsOf(mock)).toContain('POST /admin/settings/branding/logo')
    expect(save).not.toHaveBeenCalled()

    await wrapper.get('[data-testid="branding-save-logo"]').trigger('click')

    expect(save.mock.calls).toEqual([[11, '/uploads/new.png']])
  })

  it('accepts a pasted URL', async () => {
    stubRouteTable({})
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    await wrapper.get('[data-testid="branding-url-favicon"]').setValue('https://cdn.example.com/f.ico')
    await wrapper.get('[data-testid="branding-save-favicon"]').trigger('click')

    expect(save.mock.calls).toEqual([[14, 'https://cdn.example.com/f.ico']])
  })

  it('shows the API sentence when the upload is refused and stages nothing', async () => {
    stubRouteTable({ 'POST /admin/settings/branding/logo': () => problem('The file is too large.', 413) })
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    await choose(wrapper.get('[data-testid="branding-file-logo"]'))
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="branding-error-logo"]').text()).toBe('The file is too large.')
    })
    expect(wrapper.find('[data-testid="branding-save-logo"]').exists()).toBe(false)
  })

  it('saves every slot to the setting of its own key', async () => {
    stubRouteTable({})
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    await wrapper.get('[data-testid="branding-url-logo"]').setValue('https://cdn.example.com/light.png')
    await wrapper.get('[data-testid="branding-url-logodark"]').setValue('https://cdn.example.com/dark.png')
    await wrapper.get('[data-testid="branding-url-logomark"]').setValue('https://cdn.example.com/mark.png')
    await wrapper.get('[data-testid="branding-url-favicon"]').setValue('https://cdn.example.com/favicon.png')
    for (const kind of ['logo', 'logodark', 'logomark', 'favicon']) {
      await wrapper.get(`[data-testid="branding-save-${kind}"]`).trigger('click')
    }

    expect(save.mock.calls).toEqual([
      [11, 'https://cdn.example.com/light.png'],
      [12, 'https://cdn.example.com/dark.png'],
      [13, 'https://cdn.example.com/mark.png'],
      [14, 'https://cdn.example.com/favicon.png'],
    ])
  })

  it('uploads the dark logo to its own endpoint and stages it under the dark field only', async () => {
    const mock = stubRouteTable({
      'POST /admin/settings/branding/logodark': () => json({ url: '/uploads/branding/logodark/abc/logo.png' }),
    })
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    await choose(wrapper.get('[data-testid="branding-file-logodark"]'))
    await vi.waitFor(() => {
      expect((wrapper.get('[data-testid="branding-url-logodark"]').element as HTMLInputElement).value)
        .toBe('/uploads/branding/logodark/abc/logo.png')
    })

    expect(requestsOf(mock)).toEqual(['POST /admin/settings/branding/logodark'])
    expect((wrapper.get('[data-testid="branding-url-logo"]').element as HTMLInputElement).value).toBe('')
  })

  it('previews the light logo on a light surface and the dark logo on a dark one', async () => {
    stubRouteTable({})
    const both: Setting[] = [
      { id: 11, key: 'portal.logo', value: '/uploads/light.png' },
      { id: 12, key: 'portal.logo.dark', value: '/uploads/dark.png' },
      { id: 13, key: 'portal.logo.mark', value: '' },
      { id: 14, key: 'portal.favicon', value: '' },
    ]
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: both })

    const light = wrapper.get('[data-testid="branding-preview-logo"]').element
    const dark = wrapper.get('[data-testid="branding-preview-logodark"]').element

    expect(light.closest('.bg-surface-paper')).not.toBeNull()
    expect(light.closest('.bg-surface-ink')).toBeNull()
    expect(dark.closest('.bg-surface-ink')).not.toBeNull()
    expect(dark.closest('.bg-surface-paper')).toBeNull()
  })

  it('shows an upload refusal under its own slot only, even with another upload running at once', async () => {
    stubRouteTable({
      'POST /admin/settings/branding/logo': () => problem('That image is too large.', 413),
      'POST /admin/settings/branding/favicon': () => json({ url: '/uploads/favicon.png' }),
    })
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    await Promise.all([
      choose(wrapper.get('[data-testid="branding-file-logo"]')),
      choose(wrapper.get('[data-testid="branding-file-favicon"]')),
    ])
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="branding-error-logo"]').exists()).toBe(true)
    })

    expect(wrapper.findAll('[data-testid^="branding-error-"]').map((node) => node.attributes('data-testid')))
      .toEqual(['branding-error-logo'])
    expect((wrapper.get('[data-testid="branding-url-favicon"]').element as HTMLInputElement).value).toBe('/uploads/favicon.png')
  })

  it('shows a refused save under its own field only', async () => {
    stubRouteTable({})
    const refusing = vi.fn<SettingSave>(async (id) =>
      id === 12 ? { saved: false, error: 'That is not a valid address.' } : { saved: true, error: null })
    const { wrapper } = await mountScreen(BrandingCard, { save: refusing, settings: rows })

    await wrapper.get('[data-testid="branding-url-logodark"]').setValue('nonsense')
    await wrapper.get('[data-testid="branding-save-logodark"]').trigger('click')
    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('That is not a valid address.')
    })

    expect(wrapper.text().split('That is not a valid address.')).toHaveLength(2)
    expect(wrapper.findAll('[data-testid^="branding-error-"]')).toHaveLength(0)
  })

  it('removes the image from the draft with the remove button', async () => {
    stubRouteTable({})
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    await wrapper.get('[data-testid="branding-remove-logodark"]').trigger('click')

    expect(wrapper.find('[data-testid="branding-preview-logodark"]').exists()).toBe(false)
    await wrapper.get('[data-testid="branding-save-logodark"]').trigger('click')
    expect(save.mock.calls).toEqual([[12, '']])
  })
})
