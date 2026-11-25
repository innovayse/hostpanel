/**
 * Mount tests for AddClientView: it renders against the real kit, sends the form without a client
 * pre-check, shows the API's own field messages under the fields, and asks before creating a client
 * as Suspended or Closed.
 */
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import UiSelect from '@/components/ui/UiSelect.vue'
import { ClientStatus } from '@/types/client'
import AddClientView from '@/views/AddClientView.vue'
import schema from '@/views/AddClientView.schema'
import { mountClientView } from '../helpers/clientViews'
import { fieldsOf } from '../helpers/schemaParity'
import { callOf, invalid, json, stubFetch } from '../helpers/fetch'

describe('AddClientView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(AddClientView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders its main action', async () => {
    const wrapper = await mountClientView(AddClientView)

    expect(wrapper.find('[data-testid="add-client-submit"]').exists()).toBe(true)
  })

  it('sends an empty form and shows the API field messages under the fields', async () => {
    const wrapper = await mountClientView(AddClientView)
    const mock = stubFetch(
      invalid('Validation failed', {
        Email: ['Email is required by the API'],
        FirstName: ['First name is required by the API'],
      }),
    )

    await wrapper.find('[data-testid="add-client-submit"]').trigger('click')
    await flushPromises()

    expect(callOf(mock).method).toBe('POST')
    expect(wrapper.text()).toContain('Email is required by the API')
    expect(wrapper.text()).toContain('First name is required by the API')
  })

  it('sends a client created as Active without asking', async () => {
    const wrapper = await mountClientView(AddClientView)
    const mock = stubFetch(json(9))

    await wrapper.find('[data-testid="add-client-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="add-client-status-confirm"]').exists()).toBe(false)
    expect(callOf(mock).method).toBe('POST')
    expect(callOf(mock).body).toMatchObject({ status: ClientStatus.Active })
  })

  it('leaves the group empty so the API chooses the default one', async () => {
    const wrapper = await mountClientView(AddClientView)
    const mock = stubFetch(json(9))

    await wrapper.find('[data-testid="add-client-submit"]').trigger('click')
    await flushPromises()

    expect(callOf(mock).body).toMatchObject({ clientGroupId: null })
  })

  it('puts the client in the picked group', async () => {
    const wrapper = await mountClientView(AddClientView)
    const mock = stubFetch(json(9))
    const picker = wrapper.findAllComponents(UiSelect).find(c => c.attributes('data-testid') === 'add-client-group')
    picker?.vm.$emit('update:modelValue', 3)
    await flushPromises()

    await wrapper.find('[data-testid="add-client-submit"]').trigger('click')
    await flushPromises()

    expect(callOf(mock).body).toMatchObject({ clientGroupId: 3 })
  })

  it.each([
    [ClientStatus.Suspended, 'cannot place orders'],
    [ClientStatus.Closed, 'Closing is permanent'],
  ])('asks before creating a %s client, with the effects spelled out, and creates only after confirming', async (status, effects) => {
    const wrapper = await mountClientView(AddClientView)
    const mock = stubFetch(json(9))
    wrapper.findAllComponents(UiSelect).find(c => c.attributes('data-testid') === 'add-client-status')?.vm.$emit('update:modelValue', status)
    await flushPromises()

    await wrapper.find('[data-testid="add-client-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="add-client-status-confirm"]').exists()).toBe(true)
    expect(wrapper.text()).toContain(effects)
    expect(mock).not.toHaveBeenCalled()

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callOf(mock).method).toBe('POST')
    expect(callOf(mock).body).toMatchObject({ status })
    expect(wrapper.find('[data-testid="add-client-status-confirm"]').exists()).toBe(false)
  })

  it('creates nothing when the operator backs out of the prompt', async () => {
    const wrapper = await mountClientView(AddClientView)
    const mock = stubFetch(json(9))
    wrapper.findAllComponents(UiSelect).find(c => c.attributes('data-testid') === 'add-client-status')?.vm.$emit('update:modelValue', ClientStatus.Closed)
    await flushPromises()
    await wrapper.find('[data-testid="add-client-submit"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="add-client-status-confirm"]').exists()).toBe(false)
  })

  it('draws the trail above the title, the clients list as a link and the page itself last', async () => {
    const wrapper = await mountClientView(AddClientView)
    const trail = wrapper.get('[data-testid="ui-breadcrumb"]')

    expect(trail.findAll('a').map((link) => link.attributes('href'))).toEqual(['/clients'])
    expect(trail.get('[data-testid="ui-page-header-crumb-current"]').text()).toBe(wrapper.get('h1').text())
    expect(wrapper.find('[data-testid="add-client-back"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
    expect(trail.element.compareDocumentPosition(wrapper.get('h1').element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('draws every section and field of its schema, with the new user credentials in the owner card', async () => {
    const wrapper = await mountClientView(AddClientView)

    for (const section of schema.columns.flat()) {
      expect(wrapper.find(`[data-testid="ui-form-section-${section.key}"]`).exists()).toBe(true)
    }
    for (const field of fieldsOf(schema)) {
      expect(wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()).toBe(true)
    }
  })

  it('swaps the credentials for the user search when an existing user is associated', async () => {
    const wrapper = await mountClientView(AddClientView)

    await wrapper.get('[data-testid="add-client-owner-existing"]').setValue(true)
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-form-field-email"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-form-field-existingUserId"]').exists()).toBe(true)
  })
})
