/**
 * Builds the body of the client update call from a loaded client.
 *
 * The update endpoint replaces the whole record, so a screen that changes one field still has to
 * send all of them. Building the rest from the loaded record here keeps that rule in one place
 * instead of in every screen that edits a single field.
 */

import type { ClientDetail } from '@/types/client'
import type { UpdateClientPayload } from '@/types/updateclientpayload'

/**
 * Turns a loaded client into the complete update payload, with some fields replaced.
 *
 * The sign-in email, the UI language and the currency are sent as null: the API leaves those
 * alone when they are null, and this record changes none of them.
 *
 * @param detail - The client as the API returned it.
 * @param changes - The fields to replace.
 * @returns A payload carrying every field of the record.
 */
export const toUpdateClientPayload = (detail: ClientDetail, changes: Partial<UpdateClientPayload>): UpdateClientPayload => ({
  email: null,
  firstName: detail.firstName,
  lastName: detail.lastName,
  companyName: detail.companyName ?? null,
  phone: detail.phone ?? null,
  address: {
    street: detail.address.street ?? null,
    address2: detail.address.address2 ?? null,
    city: detail.address.city ?? null,
    state: detail.address.state ?? null,
    postCode: detail.address.postCode ?? null,
    country: detail.address.country ?? null,
  },
  preferences: {
    language: null,
    currency: null,
    paymentMethod: detail.preferences.paymentMethod ?? null,
    billingContact: detail.preferences.billingContact ?? null,
    adminNotes: detail.preferences.adminNotes ?? null,
  },
  notifications: { ...detail.notifications },
  settings: { ...detail.settings },
  status: null,
  // The update replaces the whole record, so the group has to travel back or the save clears it.
  clientGroupId: detail.clientGroupId ?? null,
  ...changes,
})
