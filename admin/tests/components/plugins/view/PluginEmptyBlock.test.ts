/**
 * Unit tests for PluginEmptyBlock: a title and a sentence drawn by the kit's empty state.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind } from '@/types/pluginview'
import { mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PluginEmptyBlock', () => {
  it('shows the title and the sentence', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Empty, title: 'No sites', text: 'Create one to begin.' }])

    const empty = wrapper.get('[data-testid="plugin-empty"]')
    expect(empty.text()).toContain('No sites')
    expect(empty.text()).toContain('Create one to begin.')
  })
})
