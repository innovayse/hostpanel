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
