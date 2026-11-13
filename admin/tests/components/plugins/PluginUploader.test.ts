/**
 * Unit tests for PluginUploader: choosing an archive shows it with its size, install emits it,
 * cancel forgets it, and a file dropped on the kit's upload zone is taken like a chosen one.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginUploader from '@/components/plugins/PluginUploader.vue'
import { createTestI18n } from '../../helpers/i18n'

/** A 2 KB archive standing in for the picked plugin. */
const zip = new File([new Uint8Array(2048)], 'plugin.zip', { type: 'application/zip' })

/**
 * Mounts the uploader.
 *
 * @returns The wrapper.
 */
const mountUploader = () => mount(PluginUploader, { global: { plugins: [createTestI18n()] } })

/**
 * Chooses a file in the picker the way a browser does.
 *
 * @param wrapper - The mounted uploader.
 * @param file - The file to choose.
 */
const choose = async (wrapper: ReturnType<typeof mountUploader>, file: File) => {
  const input = wrapper.get('[data-testid="plugin-zip-input"]')
  Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
  await input.trigger('change')
}

describe('PluginUploader', () => {
  it('limits the picker to zip archives and shows no install button yet', () => {
    const wrapper = mountUploader()

    expect(wrapper.get('[data-testid="plugin-zip-input"]').attributes('accept')).toBe('.zip')
    expect(wrapper.find('[data-testid="plugin-install"]').exists()).toBe(false)
  })

  it('shows the chosen archive and emits it on install', async () => {
    const wrapper = mountUploader()

    await choose(wrapper, zip)
    expect(wrapper.get('[data-testid="plugin-selected-name"]').text()).toBe('plugin.zip')
    expect(wrapper.text()).toContain('2.0 KB')

    await wrapper.get('[data-testid="plugin-install"]').trigger('click')
    expect(wrapper.emitted('upload')).toEqual([[zip]])
    expect(wrapper.find('[data-testid="plugin-install"]').exists()).toBe(false)
  })

  it('forgets the archive on cancel', async () => {
    const wrapper = mountUploader()

    await choose(wrapper, zip)
    await wrapper.get('[data-testid="plugin-cancel"]').trigger('click')

    expect(wrapper.find('[data-testid="plugin-selected-name"]').exists()).toBe(false)
  })

  it('takes a dropped file like a chosen one', async () => {
    const wrapper = mountUploader()

    await wrapper.get('label').trigger('drop', { dataTransfer: { files: [zip] } })

    expect(wrapper.get('[data-testid="plugin-selected-name"]').text()).toBe('plugin.zip')
  })
})
