/**
 * Unit tests for PluginUpdateChanges: the notes of every version an update passes through, newest first, the
 * sentence for a version without notes, the fresh install case, and nothing for a target that is not newer.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginUpdateChanges from '@/components/plugins/details/PluginUpdateChanges.vue'
import { createTestI18n } from '../../../helpers/i18n'
import { makeVersion } from '../../../helpers/pluginDetails'

/** Versions of the fixture plugin: 1.0.0 to 3.0.0, with notes on all but 2.0.0. */
const versions = [
  makeVersion({ version: '3.0.0', changelog: '- Faster **refunds**' }),
  makeVersion({ version: '2.0.0' }),
  makeVersion({ version: '1.1.0', changelog: 'Fixed a rounding error.' }),
  makeVersion({ version: '1.0.0', changelog: 'First release.' }),
]

/**
 * Mounts the list.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountChanges = (props: Record<string, unknown> = {}) =>
  mount(PluginUpdateChanges, {
    props: { versions, installedVersion: '1.0.0', targetVersion: '3.0.0', ...props },
    global: { plugins: [createTestI18n()] },
  })

describe('PluginUpdateChanges', () => {
  it('lists every version after the installed one up to the target, newest first, with its notes', () => {
    const wrapper = mountChanges()
    const shown = wrapper.findAll('[data-testid^="plugin-update-change-"]').map((node) => node.attributes('data-testid'))

    expect(wrapper.get('[data-testid="plugin-update-changes-heading"]').text()).toBe('What changes from 1.0.0 to 3.0.0')
    expect(shown).toEqual(['plugin-update-change-3.0.0', 'plugin-update-change-2.0.0', 'plugin-update-change-1.1.0'])
    expect(wrapper.get('[data-testid="plugin-update-change-3.0.0"] li strong').text()).toBe('refunds')
    expect(wrapper.get('[data-testid="plugin-update-change-1.1.0"]').text()).toContain('Fixed a rounding error.')
  })

  it('says so for an intermediate version that has no notes', () => {
    const wrapper = mountChanges()

    expect(wrapper.get('[data-testid="plugin-update-change-2.0.0"]').text()).toContain('No release notes.')
  })

  it('stops at the target when it is not the newest version', () => {
    const wrapper = mountChanges({ targetVersion: '1.1.0' })

    expect(wrapper.find('[data-testid="plugin-update-change-3.0.0"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-update-change-1.1.0"]').exists()).toBe(true)
  })

  it('shows only the chosen version when nothing is installed', () => {
    const wrapper = mountChanges({ installedVersion: null, targetVersion: '2.0.0' })

    expect(wrapper.get('[data-testid="plugin-update-changes-heading"]').text()).toBe('Release notes of 2.0.0')
    expect(wrapper.findAll('[data-testid^="plugin-update-change-"]')).toHaveLength(1)
  })

  it('draws nothing when the target is not newer than the installed version', () => {
    const wrapper = mountChanges({ installedVersion: '3.0.0', targetVersion: '2.0.0' })

    expect(wrapper.find('[data-testid="plugin-update-changes"]').exists()).toBe(false)
  })
})
