/**
 * The kind of service a catalogue product is, as the backend's `ProductType` serialises.
 *
 * @module types/producttype
 */

/** A product's service type; the values are the names `ProductType` is written with on the wire. */
export enum ProductType {
  /** Shared web hosting plan. */
  SharedHosting = 'SharedHosting',
  /** Virtual private server. */
  Vps = 'Vps',
  /** Dedicated server. */
  Dedicated = 'Dedicated',
  /** Domain name registration. */
  Domain = 'Domain',
  /** SSL certificate. */
  Ssl = 'Ssl',
  /** Other or custom service. */
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
