/**
 * Permission flags matching the C# backend `ClientPermission` enum.
 *
 * Each value is a power-of-two bit flag. Combine with bitwise OR to represent
 * multiple permissions (e.g. `ViewDomains | ViewPayInvoices`).
 *
 * @module composables/usePermissions
 */

/** Permission bit flags of a client account. */
export enum Permission {
  /** No permissions granted. */
  None = 0,
  /** Can edit the primary client profile. */
  ModifyMasterProfile = 1,
  /** Can view and manage sub-contacts. */
  ViewManageContacts = 2,
  /** Can view products and services. */
  ViewProductsServices = 4,
  /** Can view and modify service passwords. */
  ViewModifyPasswords = 8,
  /** Can use single sign-on into services. */
  AllowSingleSignOn = 16,
  /** Can view domains. */
  ViewDomains = 32,
  /** Can manage domain settings (DNS, WHOIS, etc.). */
  ManageDomainSettings = 64,
  /** Can view and pay invoices. */
  ViewPayInvoices = 128,
  /** Can view and accept quotes. */
  ViewAcceptQuotes = 256,
  /** Can view and open support tickets. */
  ViewOpenSupportTickets = 512,
  /** Can view and manage affiliate account. */
  ViewManageAffiliate = 1024,
  /** Can view notification emails. */
  ViewEmails = 2048,
  /** Can place new orders. */
  PlaceNewOrders = 4096,
  /** All permissions: owner default. */
  All = 8191,
}

/**
 * Gives access to the {@link Permission} constants.
 *
 * Deciding whether the signed-in person holds a flag is not done here: that is
 * `useClientStore().mayDo(flag)` (built on `mayPerform`), the one rule every section, the checkout
 * and the navigation share, so a link and the action behind it cannot disagree.
 *
 * @returns The {@link Permission} constants.
 */
export const usePermissions = () => ({ Permission })
