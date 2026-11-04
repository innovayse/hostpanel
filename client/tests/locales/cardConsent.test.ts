/**
 * Ties the wording of the automatic-charge agreement to its version.
 *
 * The backend stores the version of the text a client agreed to (`CardChargeConsent.CurrentTextVersion`),
 * so a change to the wording of `client.payment.consent` in any language must come with a new version.
 * The table below holds the fingerprint of the English, Russian and Armenian wording for each version
 * ever shipped.
 *
 * @module tests/locales/cardConsent.test
 */

import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { APP_LOCALES } from '~/constants/appLocales'

/** The backend file that declares the current version of the agreement text. */
const CONSENT_SOURCE = resolve(import.meta.dirname, '../../../backend/src/Innovayse.Hostpanel.Domain/Billing/CardChargeConsent.cs')

/** SHA-256 of the agreement in every language (joined by a newline, in `APP_LOCALES` order), by text version. */
const CONSENT_HASHES: Readonly<Record<string, string>> = {
  'card-consent-2026-11-1': '856c8fe3fc85dcbc8551ddf6ee040c48428d1b3947cdbb8df929d450e3cd1bb9'
}

/**
 * Reads the agreement text of one language.
 *
 * @param locale - Locale directory name.
 * @returns The `client.payment.consent` message.
 */
const consentOf = (locale: string): string => {
  const messages = JSON.parse(readFileSync(resolve(import.meta.dirname, '../../locales', locale, 'client.json'), 'utf8')) as {
    client: { payment: { consent: string } }
  }
  return messages.client.payment.consent
}

describe('the automatic-charge agreement text', () => {
  it('has a fingerprint for the version the backend records, and the wording still matches it', () => {
    const version = /CurrentTextVersion\s*=\s*"([^"]+)"/.exec(readFileSync(CONSENT_SOURCE, 'utf8'))?.[1]
    expect(version, 'CardChargeConsent.CurrentTextVersion was not found in the backend').toBeDefined()

    const hash = createHash('sha256').update(APP_LOCALES.map(consentOf).join('\n')).digest('hex')

    expect(
      CONSENT_HASHES[version as string] === hash,
      `the consent text changed: bump CardChargeConsent.CurrentTextVersion and add the new hash (${hash}) for the new version to CONSENT_HASHES`
    ).toBe(true)
  })
})
