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
