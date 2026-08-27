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
