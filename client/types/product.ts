/**
 * Product type - represents a SaaS product by Innovayse
 * Used in Products section and Products page
 * Text content (name, tagline, description, features, pricing) loaded from i18n
 */
export interface Product {
  /** Unique identifier - used as key for i18n lookup */
  id: string
  /** Lucide icon name */
  icon: string
  /** Pricing tier keys for i18n lookup */
  pricingKeys: string[]
  /** Optional demo URL */
  demoUrl?: string
  /** Link to product detail page */
  learnMoreUrl: string
  /** Brand color for gradient/accent */
  color: string
  /** Product screenshot/mockup image URL */
  image: string
}
