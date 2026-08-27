/**
 * Project type - represents a portfolio case study
 * Used in Portfolio section and Portfolio page
 * Text content (title, description, industry, task, process, results, metrics, features, duration, teamSize, testimonial) loaded from i18n
 */
export interface Project {
  /** Unique identifier - used as key for i18n lookup */
  id: string
  /** Project categories for filtering - supports multiple categories */
  category: ('development' | 'seo' | 'ecommerce' | 'saas')[]
  /** Tech stack used */
  technologies: string[]
  /** Metric keys for i18n lookup */
  metricKeys?: string[]
  /** Metric icons mapped by key */
  metricIcons?: Record<string, string>
  /** Whether project has a testimonial in locale files */
  hasTestimonial?: boolean
  /** Testimonial author name (not translated) */
  testimonialAuthor?: string
  /** Testimonial company name (not translated) */
  testimonialCompany?: string
  /** Screenshot/image URLs for gallery */
  images: string[]
  /** Project completion year */
  year: number
  /** Live project URL */
  url?: string
}
