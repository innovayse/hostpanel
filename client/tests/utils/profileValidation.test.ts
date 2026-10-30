/**
 * Unit tests for the profile form checks: blank values, the email shape and the reasons a draft
 * is refused with.
 */
import { describe, expect, it } from 'vitest'
import { isBlank, isEmailAddress, validateEmailChange } from '~/utils/profileValidation'

describe('isBlank', () => {
  it('treats empty, whitespace and absent values as blank', () => {
    expect(isBlank('')).toBe(true)
    expect(isBlank('   ')).toBe(true)
    expect(isBlank(null)).toBe(true)
    expect(isBlank(undefined)).toBe(true)
    expect(isBlank(' a ')).toBe(false)
  })
})

describe('isEmailAddress', () => {
  it('accepts an address with a name, a domain and a dot', () => {
    expect(isEmailAddress('jane@example.com')).toBe(true)
    expect(isEmailAddress(' jane@example.com ')).toBe(true)
  })

  it('refuses an address without an at sign, a domain dot, or with spaces', () => {
    expect(isEmailAddress('jane.example.com')).toBe(false)
    expect(isEmailAddress('jane@example')).toBe(false)
    expect(isEmailAddress('ja ne@example.com')).toBe(false)
  })
})

describe('validateEmailChange', () => {
  it('passes a well-formed address with a password', () => {
    expect(validateEmailChange({ newEmail: 'new@example.com', currentPassword: 'pw' })).toEqual({})
  })

  it('names a blank address and a missing password', () => {
    expect(validateEmailChange({ newEmail: ' ', currentPassword: '' })).toEqual({
      newEmail: 'client.profile.validation.emailRequired',
      currentPassword: 'client.emailChange.passwordRequired'
    })
  })

  it('names a malformed address', () => {
    expect(validateEmailChange({ newEmail: 'jane@', currentPassword: 'pw' }))
      .toEqual({ newEmail: 'client.profile.validation.emailInvalid' })
  })
})
