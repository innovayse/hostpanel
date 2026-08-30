/**
 * GET /api/portal/client/addresses
 *
 * Returns the billing addresses on the authenticated client's account.
 *
 * There is no address collection in this backend and there never was one: this route called
 * `/clients/me/addresses`, a path `ClientProfileController` does not declare, so the billing
 * address picker 404'd on every open. The addresses a WHMCS install keeps in a table of their
 * own are two things here, and both already ship on `GET /clients/me`:
 *
 * - the account's own billing address, the `street` / `city` / `postCode` / `country` fields
 *   on the profile itself, and
 * - one address per additional contact, on `profile.contacts`.
 *
 * So this composes the list from the profile rather than asking for a second copy of data the
 * profile already answers with. The profile row keeps the `profile_<id>` id the account page
 * already uses to mean "the account's own address".
 */

/** One additional contact, as `ClientDto.Contacts` carries it. */
interface BackendContact {
  /** Contact primary key -- the id the picker selects on. */
  id: number
  /** Contact first name. */
  firstName: string
  /** Contact last name. */
  lastName: string
  /** Optional company name. */
  companyName?: string | null
  /** Street address. */
  street?: string | null
  /** Second address line. */
  address2?: string | null
  /** City. */
  city?: string | null
  /** State or region. */
  state?: string | null
  /** Postal code. */
  postCode?: string | null
  /** ISO 3166-1 alpha-2 country code. */
  country?: string | null
  /** Phone number. */
  phone?: string | null
}

/** The subset of `GET /clients/me` this route reads. */
interface BackendProfile {
  /** Client primary key. */
  id: number
  /** Account first name. */
  firstName: string
  /** Account last name. */
  lastName: string
  /** Optional company name. */
  companyName?: string | null
  /** Billing street address. */
  street?: string | null
  /** Second address line. */
  address2?: string | null
  /** Billing city. */
  city?: string | null
  /** Billing state or region. */
  state?: string | null
  /** Billing postcode. */
  postCode?: string | null
  /** Billing country code. */
  country?: string | null
  /** Account phone number. */
  phone?: string | null
  /** Additional contacts, each of which carries an address of its own. */
  contacts?: BackendContact[]
}

/** One selectable billing address, in the shape the account page renders. */
interface SavedAddress {
  /** `profile_<clientId>` for the account's own address, the contact id otherwise. */
  id: string
  /** First name on the address. */
  firstname: string
  /** Last name on the address. */
  lastname: string
  /** Company name, when there is one. */
  companyname?: string
  /** Street address. */
  address1?: string
  /** Second address line. */
  address2?: string
  /** City. */
  city?: string
  /** State or region. */
  state?: string
  /** Postal code. */
  postcode?: string
  /** ISO 3166-1 alpha-2 country code. */
  country?: string
  /** Phone number. */
  phonenumber?: string
}

export default defineEventHandler(async (event): Promise<SavedAddress[]> => {
  const profile = await internalApiCall<BackendProfile>(event, '/clients/me')

  const addresses: SavedAddress[] = [{
    id: `profile_${profile.id}`,
    firstname: profile.firstName,
    lastname: profile.lastName,
    companyname: profile.companyName ?? undefined,
    address1: profile.street ?? undefined,
    address2: profile.address2 ?? undefined,
    city: profile.city ?? undefined,
    state: profile.state ?? undefined,
    postcode: profile.postCode ?? undefined,
    country: profile.country ?? undefined,
    phonenumber: profile.phone ?? undefined
  }]

  // A contact with no street is a notification address, not a postal one -- listing it would
  // offer the reader a billing address consisting of a name.
  for (const contact of profile.contacts ?? []) {
    if (!contact.street) continue

    addresses.push({
      id: String(contact.id),
      firstname: contact.firstName,
      lastname: contact.lastName,
      companyname: contact.companyName ?? undefined,
      address1: contact.street ?? undefined,
      address2: contact.address2 ?? undefined,
      city: contact.city ?? undefined,
      state: contact.state ?? undefined,
      postcode: contact.postCode ?? undefined,
      country: contact.country ?? undefined,
      phonenumber: contact.phone ?? undefined
    })
  }

  return addresses
})
