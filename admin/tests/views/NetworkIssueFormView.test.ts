/**
 * Smoke tests for NetworkIssueFormView: create posts the form with enum defaults, edit loads the
 * issue and shows the status field, through the real store and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import NetworkIssueFormView from '@/views/NetworkIssueFormView.vue'
import schema, { NETWORK_ISSUE_STATUS_FIELD } from '@/views/NetworkIssueFormView.schema'
import { NetworkIssuePriority, NetworkIssueStatus, NetworkIssueType } from '@/types/networkissue'
import { callOf, json, problem } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'
import { toastMessages } from '../helpers/toasts'

/** The issue the edit-mode tests load. */
const issue = {
  id: 2,
  title: 'Packet loss',
  type: NetworkIssueType.Other,
  server: null,
  priority: NetworkIssuePriority.High,
  status: NetworkIssueStatus.Scheduled,
  startDate: '2026-10-01T08:00:00Z',
  endDate: null,
  description: 'Maintenance',
  createdAt: '2026-10-01T08:00:00Z',
}

/** Where the edit-mode tests open the form. */
const editRoute = { path: '/support/network-issues/2/edit', pattern: '/support/network-issues/:id/edit' }

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('NetworkIssueFormView', () => {
  it('hides the status field on create and posts the form with its defaults', async () => {
    const mock = stubPathRoutes([{ path: '/network-issues', body: 5 }])
    const wrapper = await mountView(NetworkIssueFormView)
    expect(wrapper.find('[data-testid="network-issue-form-status"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="network-issue-form-save"]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="network-issue-form-title"]').setValue('Packet loss')
    await wrapper.get('[data-testid="network-issue-form-save"]').trigger('click')
    await flushPromises()

    expect(callOf(mock)).toMatchObject({
      url: '/api/network-issues',
      method: 'POST',
      body: {
        title: 'Packet loss',
        type: NetworkIssueType.Server,
        server: null,
        priority: NetworkIssuePriority.Medium,
        status: NetworkIssueStatus.Reported,
        endDate: null,
      },
    })
  })

  it('loads the issue in edit mode, shows the status field and updates it', async () => {
    const mock = stubPathRoutes([
      {
        path: '/network-issues/2',
        body: {
          id: 2,
          title: 'Packet loss',
          type: NetworkIssueType.Other,
          server: null,
          priority: NetworkIssuePriority.High,
          status: NetworkIssueStatus.Scheduled,
          startDate: '2026-10-01T08:00:00Z',
          endDate: null,
          description: 'Maintenance',
          createdAt: '2026-10-01T08:00:00Z',
        },
      },
    ])
    const wrapper = await mountView(NetworkIssueFormView, {
      path: '/support/network-issues/2/edit',
      pattern: '/support/network-issues/:id/edit',
    })

    expect(wrapper.find('[data-testid="network-issue-form-status"]').exists()).toBe(true)
    expect((wrapper.get('[data-testid="network-issue-form-title"]').element as HTMLInputElement).value).toBe('Packet loss')

    await wrapper.get('[data-testid="network-issue-form-save"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1)).toMatchObject({
      url: '/api/network-issues/2',
      method: 'PUT',
      body: { type: NetworkIssueType.Other, priority: NetworkIssuePriority.High, status: NetworkIssueStatus.Scheduled },
    })
  })

  it('toasts the API sentence when the save is refused', async () => {
    stubRoutes({ 'POST /api/network-issues': () => problem('Title is taken.', 409) })
    const wrapper = await mountView(NetworkIssueFormView)

    await wrapper.get('[data-testid="network-issue-form-title"]').setValue('Packet loss')
    await wrapper.get('[data-testid="network-issue-form-save"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toEqual(['Title is taken.'])
  })

  it('offers a retry instead of the form when the issue cannot be loaded', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/network-issues/2': () => {
        calls += 1
        return calls === 1 ? problem('No such issue.', 404) : json(issue)
      },
    })
    const wrapper = await mountView(NetworkIssueFormView, editRoute)
    await flushPromises()

    expect(wrapper.get('[data-testid="network-issue-form-error"]').text()).toContain('No such issue.')
    expect(wrapper.find('[data-testid="network-issue-form-title"]').exists()).toBe(false)

    await wrapper.get('[data-testid="network-issue-form-error-retry"]').trigger('click')
    await flushPromises()

    expect((wrapper.get('[data-testid="network-issue-form-title"]').element as HTMLInputElement).value).toBe('Packet loss')
  })

  it('draws the fields of its schema, the status only in edit mode', async () => {
    stubPathRoutes([{ path: '/network-issues', body: 5 }])
    const wrapper = await mountView(NetworkIssueFormView)

    for (const field of schema.columns.flat().flatMap((section) => section.fields)) {
      const present = wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()
      expect(present).toBe(field.key !== NETWORK_ISSUE_STATUS_FIELD)
    }
  })

  it('keeps the fields of its schema as placeholders while the issue loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountView(NetworkIssueFormView, editRoute)

    expect(wrapper.findAll('[data-testid="ui-form-section-issue"][aria-busy="true"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="network-issue-form-title"]').exists()).toBe(false)
  })
})
