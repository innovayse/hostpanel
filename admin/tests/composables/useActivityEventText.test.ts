/**
 * Tests for the words of a notification-feed event: a title from the type and the facts, a detail line with the
 * client and the amount, and a time in the active locale.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent } from 'vue'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import { useActivityEventText } from '@/composables/useActivityEventText'
import { messages } from '@/locales/messages'
import { activityEvent, domainExpiring, overdueInvoice, paymentReceived, ticketOpened } from '../helpers/activityEvents'

/** Moment the relative wording is judged against. */
const NOW = Date.parse('2026-10-14T12:00:00Z')

/**
 * Runs the composable inside a component of the given locale.
 *
 * @param locale - Interface language.
 * @returns The title, detail and time functions.
 */
const textIn = (locale: string): ReturnType<typeof useActivityEventText> => {
  let result: ReturnType<typeof useActivityEventText> | undefined
  const Probe = defineComponent({
    setup: () => {
      result = useActivityEventText()
      return () => null
    },
  })
  mount(Probe, { global: { plugins: [createI18n({ legacy: false, locale, fallbackLocale: 'en', messages })] } })
  if (result === undefined) {
    throw new Error('The composable did not run.')
  }
  return result
}

describe('useActivityEventText', () => {
  it('words each type from its facts, in English', () => {
    const text = textIn('en')

    expect(text.title(activityEvent())).toBe('New client registered')
    expect(text.title(overdueInvoice)).toBe('Invoice #33 overdue')
    expect(text.title(paymentReceived)).toBe('Payment received for invoice #34')
    expect(text.title(ticketOpened)).toBe('New ticket: Site is down')
    expect(text.title(domainExpiring)).toBe('example.com expires soon')
  })

  it('words the same event in Armenian, not in English', () => {
    const title = textIn('hy').title(overdueInvoice)

    expect(title).toMatch(/[Ա-֏]/)
    expect(title).toContain('33')
    expect(title).not.toContain('overdue')
  })

  it('puts the client and the amount on the detail line, and the client alone when there is no amount', () => {
    const text = textIn('en')

    expect(text.detail(overdueInvoice)).toBe('Ada Lovelace · $40.00 USD')
    expect(text.detail(ticketOpened)).toBe('Ada Lovelace')
    expect(text.detail(activityEvent({ clientName: '' }))).toBe('')
  })

  it('words a past moment relatively and a coming one as a date', () => {
    const text = textIn('en')

    expect(text.time(activityEvent({ occurredAt: '2026-10-14T09:00:00Z' }), NOW)).toBe('3 hours ago')
    expect(text.time(activityEvent({ occurredAt: '2026-12-05T09:00:00Z' }), NOW)).toBe('On Dec 5')
  })

  it('words the time in the active locale', () => {
    const time = textIn('ru').time(activityEvent({ occurredAt: '2026-10-14T09:00:00Z' }), NOW)

    expect(time).toMatch(/[а-я]/)
  })
})
