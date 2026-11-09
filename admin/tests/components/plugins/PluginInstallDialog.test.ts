/**
 * Mount tests for PluginInstallDialog: each requested kind of hosting access and each capability is listed in words, the
 * changelog is shown (or a sentence that there is none), the install is confirmed or cancelled through its events, and a
 * package outside this server's version range shows why and offers no install button.
 */
import { describe, expect, it } from 'vitest'
import PluginInstallDialog from '@/components/plugins/PluginInstallDialog.vue'
import { PluginCapability, PluginHostAccess } from '@/types/plugin'
import type { PluginPackagePreviewDto } from '@/types/plugin'
import { mountWithApp } from '../../helpers/mount'

/**
 * Builds the preview of a package that asks for SSH access and serves pages, inside this server's range.
 *
 * @param overrides - Members to replace.
 * @returns The preview.
 */
const previewOf = (overrides: Partial<PluginPackagePreviewDto> = {}): PluginPackagePreviewDto => ({
  id: 'example-backup',
  name: 'Example Backup',
  version: '1.1.0',
  author: 'Example',
  description: 'Backups',
  hostAccess: [PluginHostAccess.Ssh],
  capabilities: [PluginCapability.Endpoints],
  changelog: 'Faster restores.',
  minHostVersion: '1.0.0',
  maxHostVersion: '3.0.0',
  hostVersion: '2.0.0',
  compatible: true,
  ...overrides,
})

/**
 * Mounts the dialog with a preview.
 *
 * @param preview - The preview to show.
 * @returns The wrapper.
 */
const mountDialog = (preview: PluginPackagePreviewDto) => mountWithApp(PluginInstallDialog, { props: { preview } })

describe('PluginInstallDialog', () => {
  it('lists the requested access and capabilities in words, with the changelog and the range', () => {
    const wrapper = mountDialog(previewOf())

    expect(wrapper.get('[data-testid="plugin-install-access-ssh"]').text()).toContain('over SSH')
    expect(wrapper.get('[data-testid="plugin-install-capability-endpoints"]').text()).toContain('pages, menus and widgets')
    expect(wrapper.get('[data-testid="plugin-install-changelog"]').text()).toBe('Faster restores.')
    expect(wrapper.get('[data-testid="plugin-install-range"]').text()).toBe('Runs on server versions 1.0.0 to 3.0.0.')
    expect(wrapper.find('[data-testid="plugin-install-incompatible"]').exists()).toBe(false)
  })

  it('says when the package asks for nothing and has no changelog', () => {
    const wrapper = mountDialog(previewOf({ hostAccess: [], capabilities: [], changelog: null, minHostVersion: null, maxHostVersion: null }))

    expect(wrapper.find('[data-testid="plugin-install-no-requests"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-install-no-changelog"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-install-range"]').exists()).toBe(false)
  })

  it('emits confirm on install and close on cancel', async () => {
    const wrapper = mountDialog(previewOf())

    await wrapper.get('[data-testid="plugin-install-confirm"]').trigger('click')
    await wrapper.get('[data-testid="plugin-install-cancel"]').trigger('click')

    expect(wrapper.emitted('confirm')).toHaveLength(1)
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('explains an incompatible package and offers no install', () => {
    const wrapper = mountDialog(previewOf({ minHostVersion: '2.1.0', maxHostVersion: null, compatible: false }))

    const warning = wrapper.get('[data-testid="plugin-install-incompatible"]').text()
    expect(warning).toContain('2.0.0')
    expect(warning).toContain('Runs on server version 2.1.0 and later.')
    expect(wrapper.find('[data-testid="plugin-install-confirm"]').exists()).toBe(false)
  })
})
