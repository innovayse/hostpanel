/**
 * Tests for StorefrontImageField: the preview and remove button, typing an address, and picking a
 * file that is uploaded through the supplied function.
 */
import { describe, expect, it, vi } from 'vitest'
import StorefrontImageField from '@/components/storefront/StorefrontImageField.vue'
import { mountScreen } from '../../helpers/mount'

/**
 * Mounts the field.
 *
 * @param modelValue - The current address.
 * @param upload - The upload function.
 * @param uploadError - The store's upload sentence.
 * @param label - The caption above the field, when it draws one.
 * @returns The wrapper.
 */
const mountField = async (modelValue: string, upload: (file: File) => Promise<string | null> = () => Promise.resolve(null), uploadError?: string, label?: string) =>
  (await mountScreen(StorefrontImageField, { modelValue, label, alt: 'Logo', testid: 'logo', upload, uploadError })).wrapper

describe('StorefrontImageField', () => {
  it('shows no preview while the address is empty and no caption unless it is given', async () => {
    const wrapper = await mountField('')

    expect(wrapper.find('[data-testid="logo-preview"]').exists()).toBe(false)
    // the one label left is the upload zone's own
    expect(wrapper.findAll('label')).toHaveLength(1)
  })

  it('draws the caption it is given and names the preview with the alternative text', async () => {
    const wrapper = await mountField('/img/a.png', undefined, undefined, 'Logo caption')

    expect(wrapper.text()).toContain('Logo caption')
    expect(wrapper.get('[data-testid="logo-preview"]').attributes('alt')).toBe('Logo')
  })

  it('previews the address and removes it with the button', async () => {
    const wrapper = await mountField('/img/a.png')
    expect(wrapper.get('[data-testid="logo-preview"]').attributes('src')).toBe('/img/a.png')
    // a dark logo must stay readable on the dark card, so the preview sits on a checkerboard it can flip
    expect(wrapper.find('[data-testid="ui-image-preview-flip"]').exists()).toBe(true)

    await wrapper.get('[data-testid="logo-remove"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([''])
  })

  it('emits the typed address', async () => {
    const wrapper = await mountField('')

    await wrapper.get('[data-testid="logo-url"]').setValue('https://cdn.example.com/x.png')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['https://cdn.example.com/x.png'])
  })

  it('uploads a picked file and emits the stored address', async () => {
    const upload = vi.fn(() => Promise.resolve('/img/up.png'))
    const wrapper = await mountField('', upload)
    const input = wrapper.get('[data-testid="logo-file"]')
    const file = new File(['x'], 'a.png')
    Object.defineProperty(input.element, 'files', { value: [file], configurable: true })

    await input.trigger('change')

    await vi.waitFor(() => {
      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['/img/up.png'])
    })
    expect(upload).toHaveBeenCalledWith(file)
  })

  it("does not show the store's upload sentence before this field has failed", async () => {
    const wrapper = await mountField('', () => Promise.resolve(null), 'The file is too large.')

    expect(wrapper.text()).not.toContain('The file is too large.')
  })

  it('keeps the address when the upload is refused and shows the sentence', async () => {
    const wrapper = await mountField('/img/old.png', () => Promise.resolve(null), 'The file is too large.')
    const input = wrapper.get('[data-testid="logo-file"]')
    Object.defineProperty(input.element, 'files', { value: [new File(['x'], 'a.png')], configurable: true })

    await input.trigger('change')

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('The file is too large.')
    })
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('stacks the drop zone under the preview on a phone instead of cutting it at the edge', async () => {
    const wrapper = await mountField('/img/a.png')
    const row = wrapper.get('.flex-wrap')

    expect(row.classes()).toContain('max-sm:flex-col')
    expect(wrapper.html()).toContain('sm:min-w-56')
  })
})
