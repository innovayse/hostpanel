/**
 * Unit tests for PluginFormBlock: every field kind carries a placeholder, the body is sent typed, a
 * refusal shows the API's field sentences, and a success shows the plugin's sentence and resets the fields.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { PluginBlockKind, PluginFieldType } from '@/types/pluginview'
import type { PluginFormBlock } from '@/types/pluginview'
import { invalid, json } from '../../../helpers/fetch'
import { ext, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Builds the form under test.
 *
 * @param resetOnSuccess - Whether a success empties the fields.
 * @returns The form block.
 */
const form = (resetOnSuccess = false): PluginFormBlock => ({
  kind: PluginBlockKind.Form,
  title: 'New site',
  fields: [
    { key: 'name', label: 'Name', type: PluginFieldType.Text, required: true },
    { key: 'pages', label: 'Pages', type: PluginFieldType.Number, placeholder: 'How many' },
    { key: 'notes', label: 'Notes', type: PluginFieldType.Textarea },
    { key: 'token', label: 'Token', type: PluginFieldType.Secret },
    { key: 'public', label: 'Public', type: PluginFieldType.Boolean },
  ],
  submit: { route: { method: 'post', path: '/sites' }, label: 'Create', resetOnSuccess },
})

describe('PluginFormBlock', () => {
  it('gives every text field a placeholder and hides a secret', async () => {
    const { wrapper } = await mountPluginView([form()])

    expect(wrapper.get('input[data-testid="plugin-form-name"]').attributes('placeholder')).toBe('Name')
    expect(wrapper.get('input[data-testid="plugin-form-pages"]').attributes('placeholder')).toBe('How many')
    expect(wrapper.get('textarea[data-testid="plugin-form-notes"]').attributes('placeholder')).toBe('Notes')
    expect(wrapper.get('input[data-testid="plugin-form-token"]').attributes('type')).toBe('password')
  })

  it('sends the typed body and shows the plugin sentence, then empties the fields', async () => {
    let sent: unknown = null
    const { wrapper } = await mountPluginView([form(true)], {
      routes: {
        [`POST ${ext('sites')}`]: (body) => {
          sent = body
          return json({ message: 'Created.' })
        },
      },
    })

    await wrapper.get('input[data-testid="plugin-form-name"]').setValue('Shop')
    await wrapper.get('input[data-testid="plugin-form-pages"]').setValue('3')
    await wrapper.get('[data-testid="plugin-form-public"]').trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(sent).toEqual({ name: 'Shop', pages: 3, notes: '', token: '', public: true })
    expect(wrapper.get('[data-testid="plugin-form-message"]').text()).toBe('Created.')
    expect((wrapper.get('input[data-testid="plugin-form-name"]').element as HTMLInputElement).value).toBe('')
  })

  it('shows the API sentence under the field it names', async () => {
    const { wrapper } = await mountPluginView([form()], {
      routes: { [`POST ${ext('sites')}`]: () => invalid('Check the fields.', { Name: ['Name is taken.'] }) },
    })

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-form-name-error"]').text()).toBe('Name is taken.')
    expect(wrapper.get('[data-testid="plugin-form-message"]').text()).toBe('Check the fields.')
  })
})
