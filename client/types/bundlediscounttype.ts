/**
 * How a product bundle is discounted, as the backend's `BundleDiscountType` serialises.
 *
 * @module types/bundlediscounttype
 */

/** A bundle's discount kind; the values are the names `BundleDiscountType` is written with on the wire. */
export enum BundleDiscountType {
  /** No discount: the bundle costs what its components cost. */
  None = 'None',
  /** A percentage of the sum, from 0 to 100. */
  Percentage = 'Percentage',
  /** A fixed amount in the bundle's discount currency. */
  Fixed = 'Fixed',
}
