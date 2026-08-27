/**
 * Testimonial type - represents a client review
 * Used in Testimonials carousel
 * Text content (text, position) loaded from i18n locale files
 */
export interface Testimonial {
  /** Unique identifier - used as key for i18n lookup */
  id: string
  /** Client name */
  name: string
  /** Company name */
  company: string
  /** Avatar image URL */
  avatar: string
  /** Rating from 1-5 */
  rating: number
}
