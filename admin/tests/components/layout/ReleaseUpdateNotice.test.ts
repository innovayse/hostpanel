/**
 * Mount tests for ReleaseUpdateNotice: shown only when an upgrade is available, with the version,
 * release notes link, copyable command, and "Copied!" feedback.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import ReleaseUpdateNotice from '@/components/layout/ReleaseUpdateNotice.vue'
import { useReleaseStatusStore } from '@/stores/releaseStatus'
import { createTestI18n } from '../../helpers/i18n'
import type { ReleaseStatusDto } from '@/types/releaseStatus'

afterEach(() => {
  vi.restoreAllMocks()
})

const BASE_STATUS: ReleaseStatusDto = {
  installedVersion: '1.0.0',
  latestVersion: '2.0.0',
  latestPublishedAt: '2026-06-01T00:00:00Z',
  releaseNotesUrl: 'https://github.com/innovayse/hostpanel/releases/tag/v2.0.0',
  upgradeCommand: 'curl -fsSL https://get.hostpanel.app | sh -s -- --version 2.0.0',
  isUpgradeAvailable: true,
}

/**
 * Mounts the notice with the given store state.
 *
 * @param status - The release status to set on the store; null means not yet fetched.
 * @returns The wrapper.
 */
const mountNotice = (status: ReleaseStatusDto | null) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  const store = useReleaseStatusStore()
  store.status = status
  return mount(ReleaseUpdateNotice, { global: { plugins: [createTestI18n(), pinia] } })
}

describe('ReleaseUpdateNotice', () => {
  it('renders nothing when the status has not been fetched', () => {
    const wrapper = mountNotice(null)
    expect(wrapper.find('[data-testid="release-update-notice"]').exists()).toBe(false)
  })

  it('renders nothing when no upgrade is available', () => {
    const wrapper = mountNotice({ ...BASE_STATUS, isUpgradeAvailable: false })
    expect(wrapper.find('[data-testid="release-update-notice"]').exists()).toBe(false)
  })

  it('shows the new version in the title', () => {
    const wrapper = mountNotice(BASE_STATUS)
    expect(wrapper.get('[data-testid="release-update-notice"]').text()).toContain('2.0.0')
  })

  it('renders the release notes link when a URL is provided', () => {
    const wrapper = mountNotice(BASE_STATUS)
    const link = wrapper.get('[data-testid="release-update-notes-link"]')
    expect(link.attributes('href')).toBe('https://github.com/innovayse/hostpanel/releases/tag/v2.0.0')
  })

  it('omits the release notes link when no URL is provided', () => {
    const wrapper = mountNotice({ ...BASE_STATUS, releaseNotesUrl: null })
    expect(wrapper.find('[data-testid="release-update-notes-link"]').exists()).toBe(false)
  })

  it('shows the upgrade command', () => {
    const wrapper = mountNotice(BASE_STATUS)
    expect(wrapper.get('[data-testid="release-update-command"]').text()).toContain('--version 2.0.0')
  })

  it('omits the command block when no command is provided', () => {
    const wrapper = mountNotice({ ...BASE_STATUS, upgradeCommand: null })
    expect(wrapper.find('[data-testid="release-update-command"]').exists()).toBe(false)
  })

  it('shows "Copied!" feedback after clicking the copy button', async () => {
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } })
    vi.useFakeTimers()

    const wrapper = mountNotice(BASE_STATUS)
    expect(wrapper.find('[data-testid="release-update-copied"]').exists()).toBe(false)

    await wrapper.get('[data-testid="release-update-copy"]').trigger('click')
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="release-update-copied"]').exists()).toBe(true)

    await vi.advanceTimersByTimeAsync(2000)
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="release-update-copied"]').exists()).toBe(false)

    vi.useRealTimers()
  })
})
