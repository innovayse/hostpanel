/** Represents an email template. */
export interface EmailTemplate {
  /** Unique template identifier. */
  id: number
  /** Template name/slug. */
  name: string
  /** Email subject line. */
  subject: string
  /** HTML body content. */
  body: string
}
