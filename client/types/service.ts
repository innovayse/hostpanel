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
