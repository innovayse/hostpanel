/**
 * Service type - represents a digital service offered by Innovayse
 * Used in Services section and Services page
 * Text content (title, description, features, branches) is loaded from i18n locale files
 */
export interface Service {
  /** Unique identifier - used as key for i18n lookup */
  id: string
  /** Lucide icon name (e.g., "code", "search") */
  icon: string
  /** Service category for filtering */
  category: 'development' | 'seo' | 'ppc' | 'other'
  /** Branch keys for i18n lookup */
  branchKeys: string[]
  /** Branch icons mapped by key */
  branchIcons: Record<string, string>
  /** Link to detailed service page */
  link: string
}

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

/**
 * FAQ type - represents a frequently asked question
 * Used in FAQ section with category tabs
 * Text content (question, answer) loaded from i18n locale files
 */
export interface FAQ {
  /** Unique identifier - used as key for i18n lookup */
  id: string
  /** Category for tab filtering */
  category: 'general' | 'development' | 'seo' | 'products'
}

/**
 * Timeline Event - represents a milestone in company history
 * Used in About page timeline
 * Text content (title, description) loaded from i18n locale files
 */
export interface TimelineEvent {
  /** Year of the event - used as key for i18n lookup */
  year: number
}

/**
 * Team Statistics - company stats displayed in Hero and About
 */
export interface TeamStats {
  /** Number of completed projects */
  projects: number
  /** Years in business */
  years: number
  /** Team size */
  teamSize: number
  /** Number of clients served */
  clients: number
}

/**
 * Partner - represents a partner/client company
 * Used in Partners section
 */
export interface Partner {
  /** Unique identifier */
  id: string
  /** Company name */
  name: string
  /** Phosphor icon name */
  icon: string
  /** Partner website URL */
  url?: string
  /** Brief description of what the company does */
  description?: string
  /** Industry/sector */
  industry?: string
}

/**
 * Process Step - represents a step in the work process
 * Used in Process section
 * Text content (title, description) loaded from i18n locale files
 */
export interface ProcessStep {
  /** Unique identifier - used as key for i18n lookup */
  id: string
  /** Phosphor icon name */
  icon: string
  /** Order in the process */
  order: number
}

