/**
 * Mount tests for EmailForwardingTable: rows, the shared add/edit row, toggle, delete prompt and
 * the outcome line of each change.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import EmailForwardingTable from '@/components/clients/EmailForwardingTable.vue'
import type { EmailForwardingRuleItem } from '@/types/domain'
import { createTestI18n } from '../../helpers/i18n'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

/**
 * Builds a forwarding rule fixture.
 *
 * @returns One active rule.
 */
const rule = (): EmailForwardingRuleItem =>
  ({ id: 3, source: 'info@', destination: 'me@example.com', isActive: true }) as EmailForwardingRuleItem

/**
 * Mounts the table for domain 4.
 *
 * @param rules - Rules to list.
 * @returns The wrapper.
 */
const mountTable = (rules: EmailForwardingRuleItem[]) =>
  mount(EmailForwardingTable, {
    props: { domainId: 4, rules },
    global: { plugins: [createTestI18n(), createPinia()], stubs: { teleport: true } },
  })

describe('EmailForwardingTable', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('lists the rules', () => {
    const wrapper = mountTable([rule()])

    expect(wrapper.findAll('tbody tr')).toHaveLength(1)
    expect(wrapper.text()).toContain('me@example.com')
  })

  it('does not save an incomplete draft', async () => {
    const fetchMock = stubFetch(json({}))
    const wrapper = mountTable([])

    await wrapper.get('[data-testid="clients-forwarding-add"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-draft-save"]').trigger('click')

    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('saves a new rule and asks for a refresh', async () => {
    const fetchMock = stubFetch(json({}))
    const wrapper = mountTable([])

    await wrapper.get('[data-testid="clients-forwarding-add"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-draft-source"]').setValue('sales@')
    await wrapper.get('[data-testid="clients-forwarding-draft-destination"]').setValue('team@example.com')
    await wrapper.get('[data-testid="clients-forwarding-draft-save"]').trigger('click')
    await flushPromises()

    expect(callOf(fetchMock).method).toBe('POST')
    expect(wrapper.emitted('refresh')).toHaveLength(1)
  })

  it('edits a rule through the same row', async () => {
    const wrapper = mountTable([rule()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-edit-3"]').trigger('click')

    expect((wrapper.get('[data-testid="clients-forwarding-draft-source"]').element as HTMLInputElement).value).toBe('info@')
  })

  it('confirms before deleting', async () => {
    const fetchMock = stubFetch(json({}))
    const wrapper = mountTable([rule()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-delete-3"]').trigger('click')
    expect(fetchMock).not.toHaveBeenCalled()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callOf(fetchMock).method).toBe('DELETE')
    expect(wrapper.emitted('refresh')).toHaveLength(1)
  })

  it('reports a deleted entry under the heading', async () => {
    stubFetch(new Response(null, { status: 204 }))
    const wrapper = mountTable([rule()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="clients-forwarding-feedback"]').text()).toBe('Forwarding rule deleted.')
  })

  it('shows the API sentence when the delete is refused and asks for no refresh', async () => {
    stubFetch(problem('The registrar refused the change.', 502))
    const wrapper = mountTable([rule()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="clients-forwarding-feedback"]').text()).toBe('The registrar refused the change.')
    expect(wrapper.emitted('refresh')).toBeUndefined()
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const fetchMock = stubFetch(json({}))
    const wrapper = mountTable([rule()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(fetchMock).not.toHaveBeenCalled()
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('reports a saved entry under the heading', async () => {
    stubFetch(json({}))
    const wrapper = mountTable([rule()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-edit-3"]').trigger('click')
    await wrapper.get('[data-testid="clients-forwarding-draft-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="clients-forwarding-feedback"]').text()).toBe('Forwarding rule saved.')
  })
})
