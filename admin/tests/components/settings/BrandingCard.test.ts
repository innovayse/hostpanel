/**
 * Tests for BrandingCard: a picked file goes through the settings store (network level), its URL
 * is staged as a draft, Save emits it, and a refusal shows the API's own sentence.
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

  it('removes the image from the draft with the remove button', async () => {
    stubRouteTable({})
    const { wrapper } = await mountScreen(BrandingCard, { save, settings: rows })

    await wrapper.get('[data-testid="branding-remove-logodark"]').trigger('click')

    expect(wrapper.find('[data-testid="branding-preview-logodark"]').exists()).toBe(false)
    await wrapper.get('[data-testid="branding-save-logodark"]').trigger('click')
    expect(save.mock.calls).toEqual([[12, '']])
  })
})
