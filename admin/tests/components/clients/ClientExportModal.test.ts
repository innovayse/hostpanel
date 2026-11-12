/**
 * Mount tests for ClientExportModal: field selection and the download request.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ClientExportModal from '@/components/clients/ClientExportModal.vue'
import { createTestI18n } from '../../helpers/i18n'
import { captureDownloads } from '../../helpers/captureDownloads'
import { json, stubFetch } from '../../helpers/fetch'

/**
 * Mounts the modal for client 7.
 *
 * @returns The wrapper.
 */
const mountModal = () =>
  mount(ClientExportModal, {
    props: { clientId: 7, clientName: 'Ada Lovelace' },
    global: { plugins: [createTestI18n(), createPinia()], stubs: { teleport: true } },
  })

describe('ClientExportModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    // Clicking the download anchor would make happy-dom navigate to the blob URL; the helper records it instead.
    captureDownloads()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('shows the client name with every field selected', () => {
    const wrapper = mountModal()

    expect(wrapper.text()).toContain('Ada Lovelace')
    expect(wrapper.get('[data-testid="clients-export-field-invoices"]').attributes('aria-checked')).toBe('true')
  })

  it('offers the linked users and invitations sections and explains how thin sections are exported', () => {
    const wrapper = mountModal()

    expect(wrapper.get('[data-testid="clients-export-field-users"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="clients-export-field-invitations"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="clients-export-storage-note"]').text()).toContain('empty list')
  })

  it('disables download when nothing is selected', async () => {
    const wrapper = mountModal()

    await wrapper.get('[data-testid="clients-export-select-all"]').trigger('click')

    expect(wrapper.get('[data-testid="clients-export-download"]').attributes('disabled')).toBeDefined()
  })

  it('requests the export and closes after downloading', async () => {
    const fetchMock = stubFetch(json({ profileData: {} }))
    const wrapper = mountModal()

    await wrapper.get('[data-testid="clients-export-download"]').trigger('click')
    await flushPromises()

    expect(fetchMock).toHaveBeenCalled()
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
