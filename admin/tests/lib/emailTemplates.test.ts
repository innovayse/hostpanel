/**
 * Tests for the email templates helpers: the group of a slug, the readable fallback name, the subject cut into text and
 * placeholder parts, and the language badge code.
 */
import { describe, expect, it } from 'vitest'
import { emailTemplateGroupOf, humanizeSlug, languageBadge, splitSubject } from '@/lib/emailTemplates'

describe('emailTemplateGroupOf', () => {
  it('is the first word of the slug', () => {
    expect(emailTemplateGroupOf('domain-expired')).toBe('domain')
    expect(emailTemplateGroupOf('welcome')).toBe('welcome')
  })
})

describe('humanizeSlug', () => {
  it('turns hyphens into spaces and capitalises the first letter', () => {
    expect(humanizeSlug('domain-registration-failed')).toBe('Domain registration failed')
    expect(humanizeSlug('welcome')).toBe('Welcome')
  })
})

describe('splitSubject', () => {
  it('cuts plain text and placeholders in reading order', () => {
    expect(splitSubject('Invoice {{ invoice.number }} is due on {{due_date}}.')).toEqual([
      { text: 'Invoice ', placeholder: false },
      { text: 'invoice.number', placeholder: true },
      { text: ' is due on ', placeholder: false },
      { text: 'due_date', placeholder: true },
      { text: '.', placeholder: false },
    ])
  })

  it('keeps a subject with no placeholder as one text part', () => {
    expect(splitSubject('Hello')).toEqual([{ text: 'Hello', placeholder: false }])
  })

  it('gives nothing for an empty subject', () => {
    expect(splitSubject('')).toEqual([])
  })
})

describe('languageBadge', () => {
  it('writes the code in capitals', () => {
    expect(languageBadge('hy')).toBe('HY')
  })
})
