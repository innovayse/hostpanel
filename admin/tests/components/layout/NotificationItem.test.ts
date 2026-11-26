/**
 * Mount tests for NotificationItem: the tinted type icon, the localised title, the client and amount line, the time,
 * the unread dot, the link to the record and the two events (open, read).
 */
import { describe, expect, it } from 'vitest'
import NotificationItem from '@/components/layout/NotificationItem.vue'
import { mountScreen } from '../../helpers/mount'
import { activityEvent, domainExpiring, overdueInvoice, paymentReceived, ticketOpened } from '../../helpers/activityEvents'

describe('NotificationItem', () => {
  it('words the title and the detail line from the type and the facts', async () => {
    const { wrapper } = await mountScreen(NotificationItem, { event: overdueInvoice, unread: true, index: 0 })

    expect(wrapper.get('[data-testid="notification-0-link"]').text()).toContain('Invoice #33 overdue')
    expect(wrapper.get('[data-testid="notification-0-detail"]').text()).toBe('Ada Lovelace · $40.00 USD')
  })

  it('frames the icon in a circle tinted with the tone of the type', async () => {
    const warning = await mountScreen(NotificationItem, { event: overdueInvoice, unread: true, index: 0 })
    const success = await mountScreen(NotificationItem, { event: paymentReceived, unread: true, index: 0 })
    const info = await mountScreen(NotificationItem, { event: ticketOpened, unread: true, index: 0 })

    expect(warning.wrapper.get('[data-testid="notification-0-icon"]').attributes('data-tone')).toBe('warning')
    expect(warning.wrapper.get('[data-testid="notification-0-icon"]').classes()).toContain('bg-tint-yellow')
    expect(success.wrapper.get('[data-testid="notification-0-icon"]').attributes('data-tone')).toBe('success')
    expect(success.wrapper.get('[data-testid="notification-0-icon"]').classes()).toContain('bg-tint-green')
    expect(info.wrapper.get('[data-testid="notification-0-icon"]').attributes('data-tone')).toBe('info')
    expect(info.wrapper.get('[data-testid="notification-0-icon"]').classes()).toContain('bg-tint-blue')
  })

  it('links the whole row to the record the event is about', async () => {
    const { wrapper } = await mountScreen(NotificationItem, { event: ticketOpened, unread: false, index: 2 })

    expect(wrapper.get('[data-testid="notification-2-link"]').attributes('href')).toBe('/support/tickets/12')
  })

  it('shows the dot, the highlight and the mark-read button while unread', async () => {
    const { wrapper } = await mountScreen(NotificationItem, { event: overdueInvoice, unread: true, index: 0 })

    expect(wrapper.get('[data-testid="notification-0"]').attributes('data-unread')).toBe('true')
    expect(wrapper.get('[data-testid="notification-0"]').classes()).toContain('bg-primary-500/5')
    expect(wrapper.find('[data-testid="notification-0-dot"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="notification-0-mark-read"]').attributes('aria-label')).toBe('Mark as read')
    expect(wrapper.text()).toContain('Unread')
  })

  it('draws a read row plain: no dot, no highlight, no button', async () => {
    const { wrapper } = await mountScreen(NotificationItem, { event: overdueInvoice, unread: false, index: 0 })

    expect(wrapper.get('[data-testid="notification-0"]').attributes('data-unread')).toBe('false')
    expect(wrapper.get('[data-testid="notification-0"]').classes()).not.toContain('bg-primary-500/5')
    expect(wrapper.find('[data-testid="notification-0-dot"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="notification-0-mark-read"]').exists()).toBe(false)
  })

  it('gives no client line when the event names neither a client nor an amount', async () => {
    const { wrapper } = await mountScreen(NotificationItem, { event: activityEvent({ clientName: '' }), unread: false, index: 0 })

    expect(wrapper.find('[data-testid="notification-0-detail"]').exists()).toBe(false)
  })

  it('words a coming moment as a date', async () => {
    const { wrapper } = await mountScreen(NotificationItem, { event: { ...domainExpiring, occurredAt: '2099-12-05T09:00:00Z' }, unread: false, index: 0 })

    expect(wrapper.get('[data-testid="notification-0-time"]').text()).toBe('On Dec 5')
  })

  it('emits open when the row is followed and read from its button', async () => {
    const { wrapper } = await mountScreen(NotificationItem, { event: overdueInvoice, unread: true, index: 0 })

    await wrapper.get('[data-testid="notification-0-link"]').trigger('click')
    await wrapper.get('[data-testid="notification-0-mark-read"]').trigger('click')

    expect(wrapper.emitted('open')).toEqual([[overdueInvoice]])
    expect(wrapper.emitted('read')).toEqual([[overdueInvoice]])
  })
})
