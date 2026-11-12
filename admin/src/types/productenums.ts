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

/** Wire names of the product fields the API's per-field messages come under. */
export enum ProductField {
  /** The stock keeping unit. */
  Sku = 'sku',
  /** The free-form metadata JSON object. */
  Metadata = 'metadata',
  /** Days a lapsed digital product stays usable. */
  GracePeriodDays = 'gracePeriodDays',
}

/** Machine-readable API error codes (`type` of a problem response) the product screens branch on. */
export enum ProductErrorCode {
  /** Another product already uses the SKU (409). */
  SkuAlreadyInUse = 'PRODUCT_SKU_ALREADY_IN_USE',
}

/** How a product bundle discounts the sum of its components; the members are the strings the API sends and accepts. */
export enum BundleDiscountType {
  /** No discount: the bundle costs what its components cost. */
  None = 'None',
  /** A percentage of the sum, from 0 to 100. */
  Percentage = 'Percentage',
  /** A fixed amount in the bundle's discount currency. */
  Fixed = 'Fixed',
}
