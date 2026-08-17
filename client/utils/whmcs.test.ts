import { describe, expect, it } from 'vitest'
import { parseDescription } from './whmcs'

/**
 * The exact shape a WHMCS-authored description arrives in when it was edited as
 * HTML: prose, then check-marked items separated by `<br />`. Printed straight into
 * a card it showed its own markup — "✔ 20 GB Disk Space <br /> ✔ 200 GB Bandwidth".
 */
const HTML_DESCRIPTION =
  'The Business Hosting plan provides the optimal performance for medium-sized '
  + 'enterprises and online stores. It includes everything you need to run a fast, '
  + 'secure, and professional website. <br /> ✔ 20 GB Disk Space <br /> ✔ 200 GB '
  + 'Bandwidth <br /> ✔ 50 Email Accounts <br /> ✔ 20 MySQL Databases <br /> '
  + '✔ 20 Subdomains <br /> ✔ 20 FTP Accounts <br /> ✔ Free SSL Certificate <br />'

describe('parseDescription', () => {
  it('separates the prose from the check-marked items in an HTML description', () => {
    const { summary, features } = parseDescription(HTML_DESCRIPTION)

    expect(summary).toBe(
      'The Business Hosting plan provides the optimal performance for medium-sized '
      + 'enterprises and online stores. It includes everything you need to run a fast, '
      + 'secure, and professional website.')
    expect(features).toEqual([
      '20 GB Disk Space',
      '200 GB Bandwidth',
      '50 Email Accounts',
      '20 MySQL Databases',
      '20 Subdomains',
      '20 FTP Accounts',
      'Free SSL Certificate',
    ])
  })

  it('leaves no markup anywhere in the result', () => {
    const { summary, features } = parseDescription(HTML_DESCRIPTION)

    for (const text of [summary, ...features]) {
      expect(text).not.toMatch(/<[^>]+>/)
      expect(text).not.toContain('&')
    }
  })

  it('handles a description that is nothing but items, with no prose', () => {
    const { summary, features } = parseDescription(
      '✔ 600 MB Disk Space <br /> ✔ 5 GB Bandwidth <br /> ✔ 2 Email Accounts')

    expect(summary).toBe('')
    expect(features).toEqual(['600 MB Disk Space', '5 GB Bandwidth', '2 Email Accounts'])
  })

  it('still reads a plain-text description, which the catalogue also holds', () => {
    const { summary, features } = parseDescription(
      'A simple plan.\n\n• 10 GB Disk\n• 1 TB Bandwidth')

    expect(summary).toBe('A simple plan.')
    expect(features).toEqual(['10 GB Disk', '1 TB Bandwidth'])
  })

  it('reports an empty description as empty rather than as one blank feature', () => {
    expect(parseDescription('')).toEqual({ summary: '', features: [] })
  })
})
