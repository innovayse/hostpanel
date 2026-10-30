/**
 * The closed value sets of a product: what it is and whether the storefront shows it.
 *
 * The API sends both as strings; the members here are the exact strings, so a comparison reads
 * `product.status === ProductStatus.Active` instead of repeating a literal.
 */

/** What a product provisions. */
export enum ProductType {
  /** A shared hosting plan. */
  SharedHosting = 'SharedHosting',
  /** A virtual private server. */
  Vps = 'Vps',
  /** A dedicated server. */
  Dedicated = 'Dedicated',
  /** A domain registration. */
  Domain = 'Domain',
  /** A certificate. */
  Ssl = 'Ssl',
  /** Anything else. */
  Other = 'Other',
  /** Mailboxes and mail hosting. */
  Email = 'Email',
  /** An extra attached to another service. */
  Addon = 'Addon',
  /** Backup storage and restore. */
  Backup = 'Backup',
  /** A digital good (a licence, a credit pack, a plan) delivered by a fulfilment plugin, not provisioned. */
  Digital = 'Digital',
}

/** Whether the storefront shows a product. */
export enum ProductStatus {
  /** Visible and orderable. */
  Active = 'Active',
  /** Hidden from the storefront. */
  Inactive = 'Inactive',
}
