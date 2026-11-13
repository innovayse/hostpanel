/**
 * Unit tests for UiUploadZone: file picked or dropped is reported, progress state, test hooks.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiUploadZone from '@/components/ui/UiUploadZone.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the zone with the English catalogue.
 *
 * @param props - Props for the zone.
 * @returns The wrapper.
 */
const mountZone = (props: InstanceType<typeof UiUploadZone>['$props'] = {}) =>
  mount(UiUploadZone, { props, global: { plugins: [createTestI18n()] } })

describe('UiUploadZone', () => {
  it('invites the user to drop or browse and shows the hint', () => {
    const wrapper = mountZone({ hint: 'PNG, max 5MB' })

    expect(wrapper.text()).toContain('Drop a file or')
    expect(wrapper.text()).toContain('browse')
    expect(wrapper.text()).toContain('PNG, max 5MB')
  })

  it('passes accept and the test id to the hidden input', () => {
    const wrapper = mountZone({ accept: 'image/png', inputTestid: 'zone-file' })
    const input = wrapper.get('[data-testid="zone-file"]')

    expect(input.attributes('type')).toBe('file')
    expect(input.attributes('accept')).toBe('image/png')
  })

  it('emits the file chosen through the picker', async () => {
    const wrapper = mountZone({ inputTestid: 'zone-file' })
    const file = new File(['x'], 'a.png', { type: 'image/png' })
    const input = wrapper.get('[data-testid="zone-file"]')
    Object.defineProperty(input.element, 'files', { value: [file], configurable: true })

    await input.trigger('change')

    expect(wrapper.emitted('select')?.[0]).toEqual([file])
  })

  it('emits nothing when the picker closes without a file', async () => {
    const wrapper = mountZone({ inputTestid: 'zone-file' })
    const input = wrapper.get('[data-testid="zone-file"]')
    Object.defineProperty(input.element, 'files', { value: [], configurable: true })

    await input.trigger('change')

    expect(wrapper.emitted('select')).toBeUndefined()
  })

  it('emits the file dropped on the zone and leaves the dragging state', async () => {
    const wrapper = mountZone()
    const file = new File(['x'], 'a.png')

    await wrapper.trigger('dragover')
    expect(wrapper.classes()).toContain('border-text-accent')

    await wrapper.trigger('drop', { dataTransfer: { files: [file] } })

    expect(wrapper.emitted('select')?.[0]).toEqual([file])
    expect(wrapper.classes()).not.toContain('border-text-accent')
  })

  it('shows the loader instead of the invitation while uploading', () => {
    const wrapper = mountZone({ uploading: true })

    expect(wrapper.get('[role="status"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.text()).not.toContain('browse')
  })

  it('keeps the input focusable (screen-reader-only) and rings the zone when it is focused', () => {
    const wrapper = mountZone()

    expect(wrapper.get('input').classes()).toContain('sr-only')
    expect(wrapper.get('input').classes()).not.toContain('hidden')
    expect(wrapper.classes()).toContain('has-focus-visible:ring-2')
  })
})
