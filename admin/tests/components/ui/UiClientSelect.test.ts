/**
 * Unit tests for UiClientSelect: shows the chosen client, filters by name, email or id, picks,
 * clears, and passes the remote lookup through.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import { createTestI18n } from '../../helpers/i18n'

/** Clients used by the tests. */
const clients = [
  { id: 1, name: 'Acme Ltd', email: 'hello@acme.test' },
  { id: 2, name: 'Beta Corp', status: 'Active' },
]

/** The mounted wrappers, unmounted after each test so their document listeners go. */
const mounted: VueWrapper[] = []

/**
 * Mounts the picker with the English catalogue.
 *
 * @param modelValue - Id of the chosen client.
 * @param extra - Further props (remote lookup, loading, selected name).
 * @returns The wrapper.
 */
const mountSelect = (modelValue: string | number | null, extra: Record<string, unknown> = {}) => {
  const wrapper = mount(UiClientSelect, {
    props: { modelValue, clients, ...extra },
    attachTo: document.body,
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })
  mounted.push(wrapper)
  return wrapper
}

afterEach(() => {
  mounted.splice(0).forEach(wrapper => wrapper.unmount())
})

describe('UiClientSelect', () => {
  it('shows the name of the chosen client', () => {
    expect(mountSelect(1).get('button').text()).toBe('Acme Ltd')
  })

  it('shows the placeholder for no choice', () => {
    expect(mountSelect(null).get('button').text()).toBe('Select a client')
    expect(mountSelect(null, { placeholder: 'All clients' }).get('button').text()).toBe('All clients')
  })

  it('lists every client with name, id, status and email behind the row that clears the choice', async () => {
    const wrapper = mountSelect(null)

    await wrapper.get('button').trigger('click')
    const rows = wrapper.findAll('[role="option"]')

    expect(rows).toHaveLength(3)
    expect(rows[1]?.text()).toContain('Acme Ltd - #1')
    expect(rows[1]?.text()).toContain('hello@acme.test')
    expect(rows[2]?.text()).toContain('Active')
  })

  it('filters by name, email or id', async () => {
    const wrapper = mountSelect(null)
    await wrapper.get('button').trigger('click')

    await wrapper.get('input').setValue('acme')
    expect(wrapper.findAll('[role="option"]')).toHaveLength(1)

    await wrapper.get('input').setValue('hello@')
    expect(wrapper.findAll('[role="option"]')).toHaveLength(1)

    await wrapper.get('input').setValue('2')
    expect(wrapper.get('[role="option"]').text()).toContain('Beta Corp')
  })

  it('says when nothing matches', async () => {
    const wrapper = mountSelect(null)
    await wrapper.get('button').trigger('click')

    await wrapper.get('input').setValue('zzz')

    expect(wrapper.text()).toContain('No clients found')
  })

  it('emits the id of the picked client as a string and closes', async () => {
    const wrapper = mountSelect(null)
    await wrapper.get('button').trigger('click')

    await wrapper.findAll('[role="option"]')[2]?.trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['2'])
    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('clears the choice with the first row', async () => {
    const wrapper = mountSelect(1)
    await wrapper.get('button').trigger('click')

    await wrapper.findAll('[role="option"]')[0]?.trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([''])
  })

  it('passes the remote lookup through: search on open, loading row, name of an unloaded client', async () => {
    const wrapper = mountSelect(9, { remote: true, loading: true, clients: [], selectedName: 'Far Away Inc' })

    expect(wrapper.get('button').text()).toBe('Far Away Inc')
    await wrapper.get('button').trigger('click')

    expect(wrapper.emitted('search')?.[0]).toEqual([''])
    expect(wrapper.find('[data-testid="ui-select-loading"]').exists()).toBe(true)
  })
})
