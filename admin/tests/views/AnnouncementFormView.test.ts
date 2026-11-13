/**
 * Tests for AnnouncementFormView: create posts the form, edit loads the announcement first and
 * updates it, an empty title blocks saving, no date picker is offered (the server owns the
 * creation date) and edit mode shows the creation day read-only.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AnnouncementFormView from '@/views/AnnouncementFormView.vue'
import { callOf, json, problem } from '../helpers/fetch'
import { mountRouted, mountView } from '../helpers/mount'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('AnnouncementFormView', () => {
  it('keeps the form fields as placeholders while the announcement loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountRouted(AnnouncementFormView, '/support/announcements/:id/edit', '/support/announcements/4/edit')

    expect(wrapper.get('[data-testid="announcement-form-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]')).toHaveLength(3)
    expect(wrapper.find('[data-testid="announcement-form-title"]').exists()).toBe(false)
  })

  it('blocks saving until there is a title, then creates the announcement', async () => {
    const mock = stubPathRoutes([{ path: '/announcements', body: 9 }])
    const wrapper = await mountView(AnnouncementFormView)
    const save = wrapper.get('[data-testid="announcement-form-save"]')
    expect(save.attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="announcement-form-title"]').setValue('Hello')
    await wrapper.get('[data-testid="announcement-form-content"]').setValue('Body text')
    await wrapper.get('[data-testid="announcement-form-published"]').trigger('click')
    await save.trigger('click')
    await flushPromises()

    expect(callOf(mock)).toMatchObject({
      url: '/api/announcements',
      method: 'POST',
      body: { title: 'Hello', content: 'Body text', isPublished: true },
    })
  })

  it('loads the announcement in edit mode and updates it', async () => {
    const mock = stubPathRoutes([
      { path: '/announcements/4', body: { id: 4, title: 'Old', content: 'Old body', isPublished: false, createdAt: '2026-10-01T00:00:00Z' } },
    ])
    const wrapper = await mountView(AnnouncementFormView, {
      path: '/support/announcements/4/edit',
      pattern: '/support/announcements/:id/edit',
    })
    const title = wrapper.get('[data-testid="announcement-form-title"]')
    expect((title.element as HTMLInputElement).value).toBe('Old')

    await title.setValue('New')
    await wrapper.get('[data-testid="announcement-form-save"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1)).toMatchObject({ url: '/api/announcements/4', method: 'PUT', body: { title: 'New', content: 'Old body', isPublished: false } })
  })
})

describe('AnnouncementFormView without a date', () => {
  it('offers no date picker on create and posts title, content and isPublished only', async () => {
    const mock = stubRoutes({ 'POST /api/announcements': () => json(4, 201) })
    const { wrapper } = await mountRouted(AnnouncementFormView, '/support/announcements/new', '/support/announcements/new')

    expect(wrapper.find('[data-testid="announcement-form-date"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="announcement-form-created-on"]').exists()).toBe(false)

    await wrapper.get('[data-testid="announcement-form-title"]').setValue('Maintenance')
    await wrapper.get('[data-testid="announcement-form-content"]').setValue('Tonight')
    await wrapper.get('[data-testid="announcement-form-save"]').trigger('click')
    await flushPromises()

    const init = mock.mock.calls[0]?.[1] as RequestInit
    expect(JSON.parse(String(init.body))).toEqual({ title: 'Maintenance', content: 'Tonight', isPublished: false })
  })

  it('shows the creation day read-only in edit mode', async () => {
    stubRoutes({
      'GET /api/announcements/9': () => json({ id: 9, title: 'Old', content: 'Body', isPublished: true, createdAt: '2026-03-04T10:00:00Z' }),
    })
    const { wrapper } = await mountRouted(AnnouncementFormView, '/support/announcements/:id', '/support/announcements/9')
    await flushPromises()

    expect((wrapper.get('[data-testid="announcement-form-created-on"]').element as HTMLInputElement).value).toContain('2026-03-04')
  })

  it('shows a refused load with a Retry instead of the form, and loads again from it', async () => {
    let refused = true
    stubRoutes({
      'GET /api/announcements/9': () =>
        refused
          ? problem('Announcement is unavailable.', 503)
          : json({ id: 9, title: 'Old', content: 'Body', isPublished: true, createdAt: '2026-03-04T10:00:00Z' }),
    })
    const { wrapper } = await mountRouted(AnnouncementFormView, '/support/announcements/:id', '/support/announcements/9')
    await flushPromises()

    expect(wrapper.get('[data-testid="announcement-form-load-error"]').text()).toBe('Announcement is unavailable.')
    expect(wrapper.find('[data-testid="announcement-form-save"]').exists()).toBe(false)

    refused = false
    await wrapper.get('[data-testid="announcement-form-load-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="announcement-form-load-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="announcement-form-save"]').exists()).toBe(true)
  })

  it('shows the API sentence next to the form when the save is refused', async () => {
    stubRoutes({ 'POST /api/announcements': () => problem('Title is taken.', 409) })
    const { wrapper } = await mountRouted(AnnouncementFormView, '/support/announcements/new', '/support/announcements/new')
    await flushPromises()

    await wrapper.get('[data-testid="announcement-form-title"]').setValue('News')
    await wrapper.get('[data-testid="announcement-form-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="announcement-form-error"]').text()).toBe('Title is taken.')
  })
})
