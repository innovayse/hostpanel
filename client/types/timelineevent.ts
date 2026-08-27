/**
 * Timeline Event - represents a milestone in company history
 * Used in About page timeline
 * Text content (title, description) loaded from i18n locale files
 */
export interface TimelineEvent {
  /** Year of the event - used as key for i18n lookup */
  year: number
}
