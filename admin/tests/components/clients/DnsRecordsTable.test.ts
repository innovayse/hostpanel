/**
 * Mount tests for DnsRecordsTable: rows, the shared add/edit row, the delete prompt and the
 * outcome line of each change.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import DnsRecordsTable from '@/components/clients/DnsRecordsTable.vue'
import type { DnsRecordItem } from '@/types/domain'
import { createTestI18n } from '../../helpers/i18n'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

/**
 * Builds a DNS record fixture.
 *
 * @returns One A record.
 */
const record = (): DnsRecordItem =>
  ({ id: 7, type: 'A', host: '@', value: '10.0.0.1', ttl: 3600, priority: null }) as DnsRecordItem

/**
 * Mounts the table for domain 4.
 *
 * @param records - Records to list.
 * @returns The wrapper.
 */
const mountTable = (records: DnsRecordItem[]) =>
  mount(DnsRecordsTable, {
    props: { domainId: 4, records },
    global: { plugins: [createTestI18n(), createPinia()], stubs: { teleport: true } },
  })

describe('DnsRecordsTable', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('lists the records', () => {
    const wrapper = mountTable([record()])

    expect(wrapper.findAll('tbody tr')).toHaveLength(1)
    expect(wrapper.text()).toContain('10.0.0.1')
  })

  it('shows the empty message without records', () => {
    expect(mountTable([]).text()).toContain('No DNS')
  })

  it('opens the add row, saves it and asks for a refresh', async () => {
    const fetchMock = stubFetch(json({}))
    const wrapper = mountTable([])

    await wrapper.get('[data-testid="clients-dns-add"]').trigger('click')
    await wrapper.get('[data-testid="clients-dns-draft-host"]').setValue('www')
    await wrapper.get('[data-testid="clients-dns-draft-value"]').setValue('10.0.0.2')
    await wrapper.get('[data-testid="clients-dns-draft-save"]').trigger('click')
    await flushPromises()

    expect(callOf(fetchMock).method).toBe('POST')
    expect(wrapper.emitted('refresh')).toHaveLength(1)
  })

  it('edits a record through the same row', async () => {
    const wrapper = mountTable([record()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-dns-edit-7"]').trigger('click')

    expect((wrapper.get('[data-testid="clients-dns-draft-value"]').element as HTMLInputElement).value).toBe('10.0.0.1')
  })

  it('confirms before deleting', async () => {
    const fetchMock = stubFetch(json({}))
    const wrapper = mountTable([record()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-dns-delete-7"]').trigger('click')
    expect(fetchMock).not.toHaveBeenCalled()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callOf(fetchMock).method).toBe('DELETE')
    expect(wrapper.emitted('refresh')).toHaveLength(1)
  })

  it('reports a deleted entry under the heading', async () => {
    stubFetch(new Response(null, { status: 204 }))
    const wrapper = mountTable([record()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-dns-delete-7"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="clients-dns-feedback"]').text()).toBe('DNS record deleted.')
  })

  it('shows the API sentence when the delete is refused and asks for no refresh', async () => {
    stubFetch(problem('The registrar refused the change.', 502))
    const wrapper = mountTable([record()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-dns-delete-7"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="clients-dns-feedback"]').text()).toBe('The registrar refused the change.')
    expect(wrapper.emitted('refresh')).toBeUndefined()
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const fetchMock = stubFetch(json({}))
    const wrapper = mountTable([record()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-dns-delete-7"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(fetchMock).not.toHaveBeenCalled()
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('reports a saved entry under the heading', async () => {
    stubFetch(json({}))
    const wrapper = mountTable([record()])

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="clients-dns-edit-7"]').trigger('click')
    await wrapper.get('[data-testid="clients-dns-draft-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="clients-dns-feedback"]').text()).toBe('DNS record saved.')
  })
})
