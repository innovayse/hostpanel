/**
 * Mount tests for ServicePanelSyncDialog: the confirmation says what is pushed and what is not and
 * hands the password choice back, and once the sync has run the dialog lists what was done about
 * each field, with the value the panel reported where there is one.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ServicePanelSyncDialog from '@/components/services/ServicePanelSyncDialog.vue'
import { PanelSyncField, PanelSyncOutcome } from '@/types/service'
import type { ServiceSyncResult } from '@/types/service'
import { createTestI18n } from '../../helpers/i18n'

/** What a sync of a service did: one field of each outcome. */
const result: ServiceSyncResult = {
  fields: [
    { field: PanelSyncField.Status, outcome: PanelSyncOutcome.InSync, panelValue: 'Active' },
    { field: PanelSyncField.Package, outcome: PanelSyncOutcome.Applied, panelValue: 'legacy' },
    { field: PanelSyncField.Domain, outcome: PanelSyncOutcome.Unsupported, panelValue: 'moved.example' },
    { field: PanelSyncField.Password, outcome: PanelSyncOutcome.Failed, panelValue: null },
    { field: PanelSyncField.IpAddress, outcome: PanelSyncOutcome.Applied, panelValue: '203.0.113.9' },
  ],
}

/**
 * Mounts the dialog with the teleport inlined.
 *
 * @param props - The dialog's props.
 * @returns The wrapper.
 */
const mountDialog = (props: { result: ServiceSyncResult | null; loading?: boolean }) =>
  mount(ServicePanelSyncDialog, { props, global: { plugins: [createTestI18n()], stubs: { teleport: true } } })

describe('ServicePanelSyncDialog', () => {
  it('asks for the confirmation first and says what is pushed and what never is', () => {
    const wrapper = mountDialog({ result: null })

    expect(wrapper.text()).toContain('Sync with panel')
    expect(wrapper.text()).toContain('The status and the package of this service are applied to the panel')
    expect(wrapper.text()).toContain('The domain and the username are never changed on the panel')
    expect(wrapper.find('[data-testid="client-service-sync-result"]').exists()).toBe(false)
  })

  it('confirms without the password unless its box is ticked', async () => {
    const wrapper = mountDialog({ result: null })

    await wrapper.get('[data-testid="client-service-sync-confirm"]').trigger('click')

    expect(wrapper.emitted('confirm')).toEqual([[false]])
  })

  it('hands the password choice back when the box is ticked', async () => {
    const wrapper = mountDialog({ result: null })

    await wrapper.get('[data-testid="client-service-sync-push-password"]').trigger('click')
    await wrapper.get('[data-testid="client-service-sync-confirm"]').trigger('click')

    expect(wrapper.emitted('confirm')).toEqual([[true]])
  })

  it('closes from cancel', async () => {
    const wrapper = mountDialog({ result: null })

    await wrapper.get('[data-testid="client-service-sync-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('lists what was done about each field once the sync has run', () => {
    const wrapper = mountDialog({ result })

    expect(wrapper.find('[data-testid="client-service-sync-confirm"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="client-service-sync-outcome-Status"]').text()).toBe('In step')
    expect(wrapper.get('[data-testid="client-service-sync-outcome-Package"]').text()).toBe('Applied')
    expect(wrapper.get('[data-testid="client-service-sync-outcome-Domain"]').text()).toBe('Cannot be reconciled')
    expect(wrapper.get('[data-testid="client-service-sync-outcome-Password"]').text()).toBe('Failed')
    expect(wrapper.get('[data-testid="client-service-sync-outcome-IpAddress"]').text()).toBe('Applied')
  })

  it('shows the value the panel reported, and none for a field it reports nothing for', () => {
    const wrapper = mountDialog({ result })

    expect(wrapper.get('[data-testid="client-service-sync-field-Package"]').text()).toContain('Panel: legacy')
    expect(wrapper.get('[data-testid="client-service-sync-field-IpAddress"]').text()).toContain('Panel: 203.0.113.9')
    expect(wrapper.get('[data-testid="client-service-sync-field-Password"]').text()).not.toContain('Panel:')
  })

  it('closes from the result', async () => {
    const wrapper = mountDialog({ result })

    await wrapper.get('[data-testid="client-service-sync-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
