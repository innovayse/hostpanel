/**
 * The billing cycles a service renews on, exactly as the backend sends them.
 *
 * Not to be mixed up with `PricingCycle`, whose members are the keys of a product's price table:
 * this one names the cycle a service the client already owns is billed on.
 *
 * @module types/billingcycle
 */

/** A billing cycle a service renews on. */
export enum BillingCycle {
  /** Billed every month. */
  Monthly = 'Monthly',
  /** Billed every three months. */
  Quarterly = 'Quarterly',
  /** Billed every six months. */
  SemiAnnual = 'SemiAnnual',
  /** Billed once a year. */
  Annual = 'Annual',
  /** Billed every two years. */
  Biennial = 'Biennial'
}
