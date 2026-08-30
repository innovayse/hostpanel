/**
 * POST /api/portal/client/addresses
 *
 * Saves a new billing address on the authenticated client's account.
 *
 * This posted to `/clients/me/addresses`, which does not exist, so every "Add Address" save
 * failed. An address is a contact here -- see `addresses.get.ts` for why -- so the new address
 * is added through `POST /clients/me/contacts`, the route that does exist and that resolves the
 * owning client from the credential rather than from the body.
 *
 * The contact is typed `Billing`: this form is reached only from the billing-address picker,
 * and a contact with no type would default to whichever enum member is first.
 */

/** One additional contact, as `GET /clients/me/contacts` returns it. */
interface BackendContact {
  /** Contact primary key. */
  id: number
}

export default defineEventHandler(async (event): Promise<{ addressid: string }> => {
  const body = await readBody<Record<string, unknown>>(event)

  await internalApiCall(event, '/clients/me/contacts', {
    method: 'POST',
    body: {
      firstName: body.firstname,
      lastName: body.lastname,
      companyName: body.companyname || null,
      email: body.email,
      phone: body.phonenumber || null,
      type: 'Billing',
      street: body.address1,
      address2: body.address2 || null,
      city: body.city,
      state: body.state,
      postCode: body.postcode,
      country: body.country
    }
  })

  // The backend answers 204 with no id, and the picker has to select what was just saved. The
  // contact list is re-read and the highest id taken: ids are a database identity sequence, so
  // the newest row is the largest. Validation of the fields themselves stays on the API -- this
  // route deliberately checks nothing the command validator already refuses.
  const contacts = await internalApiCall<BackendContact[]>(event, '/clients/me/contacts')
  const newest = contacts.reduce<number>((max, c) => (c.id > max ? c.id : max), 0)

  return { addressid: String(newest) }
})
