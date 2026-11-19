/**
 * Mount tests of EmailTemplatesView (email templates): it lists one row per template with its languages, opens
 * the language versions for editing, shows the empty state where it has one, and shows the API's sentence when
 * the load is refused.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import EmailTemplatesView from '@/views/EmailTemplatesView.vue'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted, mountRoutedSettled } from '../helpers/mount'
import { json, problem, stubFetch } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import schema from '@/views/EmailTemplatesView.schema'
import { flushPromises } from '@vue/test-utils'

/** Two language versions of one template, as the API lists them. */
const versions = [
  { id: 3, slug: 'welcome', language: 'en', subject: 'Hello', body: '<p>Hi</p>', description: null, isActive: true },
  { id: 4, slug: 'welcome', language: 'ru', subject: 'Привет', body: '<p>Привет</p>', description: null, isActive: true },
]

/** A template of another group, with a placeholder in its subject. */
const domainExpiring = {
  id: 9, slug: 'domain-expiring', language: 'en', subject: 'Your domain {{ domain.name }} expires soon', body: '<p>Soon</p>', description: null, isActive: true,
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('EmailTemplatesView', () => {
  it('lists one row per template with the English subject and every language it has', async () => {
    stubRoutes({ 'GET /api/admin/email-templates': () => json(versions) })
    const wrapper = await mountRoutedSettled(EmailTemplatesView, '/settings/email-templates')

    const row = wrapper.get('[data-testid="email-templates-row-welcome"]')
    expect(row.text()).toContain('Hello')
    expect(row.text()).not.toContain('Привет')
    expect(wrapper.find('[data-testid="email-templates-row-welcome-en"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="email-templates-row-welcome-ru"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="email-templates-row-welcome-hy"]').exists()).toBe(false)
  })

  it('shows the readable name with the key beneath it, language codes and subject placeholders as chips', async () => {
    stubRoutes({ 'GET /api/admin/email-templates': () => json([...versions, domainExpiring]) })
    const wrapper = await mountRoutedSettled(EmailTemplatesView, '/settings/email-templates')

    const row = wrapper.get('[data-testid="email-templates-row-domain-expiring"]')
    expect(wrapper.get('[data-testid="email-templates-name-domain-expiring"]').text()).toBe('Domain expiring')
    expect(row.text()).toContain('domain-expiring')
    expect(row.get('[data-testid="email-templates-placeholder-domain-expiring"]').text()).toBe('domain.name')
    expect(row.get('[data-testid="email-templates-row-domain-expiring-en"]').text()).toBe('EN')
    expect(wrapper.get('[data-testid="email-templates-name-welcome"]').text()).toBe('Welcome')
    expect(wrapper.get('[data-testid="email-templates-row-welcome-ru"]').text()).toBe('RU')
  })

  it('narrows the list by the search text in the address, on the name, the key and the subject', async () => {
    stubRoutes({ 'GET /api/admin/email-templates': () => json([...versions, domainExpiring]) })
    const byName = await mountRouted(EmailTemplatesView, '/settings/email-templates', '/settings/email-templates?search=expiring')
    await flushPromises()
    const bySubject = await mountRouted(EmailTemplatesView, '/settings/email-templates', '/settings/email-templates?search=hello')
    await flushPromises()

    expect(byName.wrapper.find('[data-testid="email-templates-row-domain-expiring"]').exists()).toBe(true)
    expect(byName.wrapper.find('[data-testid="email-templates-row-welcome"]').exists()).toBe(false)
    expect(bySubject.wrapper.find('[data-testid="email-templates-row-welcome"]').exists()).toBe(true)
    expect(bySubject.wrapper.find('[data-testid="email-templates-row-domain-expiring"]').exists()).toBe(false)
  })

  it('narrows the list by the group chosen in the filter dialog', async () => {
    stubRoutes({ 'GET /api/admin/email-templates': () => json([...versions, domainExpiring]) })
    const { wrapper, router } = await mountRouted(EmailTemplatesView, '/settings/email-templates', '/settings/email-templates')
    await flushPromises()

    await applyListFilter(wrapper, { 'email-templates-filter-group': 'domain' })

    expect(router.currentRoute.value.query).toEqual({ group: 'domain' })
    expect(wrapper.find('[data-testid="email-templates-row-domain-expiring"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="email-templates-row-welcome"]').exists()).toBe(false)
  })

  it('says so when the search matches no template', async () => {
    stubRoutes({ 'GET /api/admin/email-templates': () => json(versions) })
    const { wrapper } = await mountRouted(EmailTemplatesView, '/settings/email-templates', '/settings/email-templates?search=zzz')
    await flushPromises()

    expect(wrapper.get('[data-testid="email-templates-no-matches"]').text()).toContain('No templates match your search')
  })

  it('adds a missing language version from the editor', async () => {
    const created = vi.fn(() => json(7))
    stubRoutes({ 'GET /api/admin/email-templates': () => json(versions), 'POST /api/admin/email-templates': created })
    const wrapper = await mountRoutedSettled(EmailTemplatesView, '/settings/email-templates')

    await wrapper.get('[data-testid="email-templates-edit-welcome"]').trigger('click')
    await wrapper.get('[data-testid="email-template-tab-hy"]').trigger('click')
    await wrapper.get('[data-testid="email-template-subject"]').setValue('Բարև')
    await wrapper.get('[data-testid="email-template-body"]').setValue('<p>Բարև</p>')
    await wrapper.get('[data-testid="email-template-form"]').trigger('submit')
    await flushPromises()

    expect(created).toHaveBeenCalledTimes(1)
  })

  it('says so when there are none', async () => {
    stubRoutes({ 'GET /api/admin/email-templates': () => json([]) })
    const wrapper = await mountRoutedSettled(EmailTemplatesView, '/settings/email-templates')

    expect(wrapper.get('[data-testid="email-templates-table"]').text()).toContain('No templates found')
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/admin/email-templates': () => problem('Not allowed.', 403) })
    const wrapper = await mountRoutedSettled(EmailTemplatesView, '/settings/email-templates')

    expect(wrapper.get('[data-testid="email-templates-error"]').text()).toContain('Not allowed.')
  })

  it('loads again from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403))
    const wrapper = await mountRoutedSettled(EmailTemplatesView, '/settings/email-templates')
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="email-templates-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })

  it('draws the table head from the schema columns while the first load is in flight', async () => {
    stubPendingFetch()
    const wrapper = await mountRoutedSettled(EmailTemplatesView, '/settings/email-templates')

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
