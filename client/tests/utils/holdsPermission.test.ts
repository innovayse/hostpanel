/**
 * Tests for `holdsPermission` (the bit test every portal visibility decision goes through) and
 * `mayPerform` (the decision itself: who the flags restrict).
 *
 * @module tests/utils/holdsPermission.test
 */

import { describe, expect, it } from 'vitest'
import { Permission } from '~/composables/usePermissions'
import { holdsPermission, mayPerform } from '~/utils/holdsPermission'

describe('holdsPermission', () => {
  it('is satisfied by a held flag and refused for one that is not held', () => {
    const held = Permission.ViewDomains | Permission.ViewEmails

    expect(holdsPermission(held, Permission.ViewDomains)).toBe(true)
    expect(holdsPermission(held, Permission.PlaceNewOrders)).toBe(false)
  })

  it('requires every flag of a combined requirement', () => {
    const required = Permission.ViewProductsServices | Permission.ViewPayInvoices

    expect(holdsPermission(Permission.ViewProductsServices, required)).toBe(false)
    expect(holdsPermission(Permission.ViewProductsServices | Permission.ViewPayInvoices, required)).toBe(true)
  })

  it('treats a requirement of none as always satisfied, even for nobody', () => {
    expect(holdsPermission(null, Permission.None)).toBe(true)
    expect(holdsPermission(undefined, Permission.None)).toBe(true)
  })

  it('grants nothing to a person with no permission value', () => {
    expect(holdsPermission(null, Permission.ViewDomains)).toBe(false)
    expect(holdsPermission(undefined, Permission.ViewDomains)).toBe(false)
    expect(holdsPermission(0, Permission.ViewDomains)).toBe(false)
  })

  it('satisfies every flag for the owner value', () => {
    expect(holdsPermission(Permission.All, Permission.ModifyMasterProfile | Permission.PlaceNewOrders)).toBe(true)
    expect(holdsPermission(Permission.All, Permission.All)).toBe(true)
  })
})

describe('mayPerform', () => {
  it('is true while nobody is loaded, so the API decides', () => {
    expect(mayPerform(null, Permission.PlaceNewOrders)).toBe(true)
    expect(mayPerform(undefined, Permission.PlaceNewOrders)).toBe(true)
  })

  it('lets a member of an account do what they hold and refuses what they do not', () => {
    const member = { permissions: Permission.ViewDomains, actsForAccount: true }

    expect(mayPerform(member, Permission.ViewDomains)).toBe(true)
    expect(mayPerform(member, Permission.PlaceNewOrders)).toBe(false)
  })

  it('lets a signed-in first-time buyer, who acts for no account and holds nothing, do anything', () => {
    const buyer = { permissions: Permission.None, actsForAccount: false }

    expect(mayPerform(buyer, Permission.PlaceNewOrders)).toBe(true)
    expect(mayPerform(buyer, Permission.ViewPayInvoices)).toBe(true)
  })

  it('treats a record that does not say whether it acts for an account as one that does', () => {
    const unknown = { permissions: Permission.None } as { permissions: number, actsForAccount: boolean }

    expect(mayPerform(unknown, Permission.PlaceNewOrders)).toBe(false)
  })
})
