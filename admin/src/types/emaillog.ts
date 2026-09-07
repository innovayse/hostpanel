/** Represents a single email log entry. */
export interface EmailLog {
  /** Unique identifier. */
  id: number
  /** Recipient email address. */
  to: string
  /** Email subject line. */
  subject: string
  /** UTC timestamp of the send attempt. */
  sentAt: string
  /** Whether the email was delivered successfully. */
  success: boolean
  /** Error message if delivery failed; null on success. */
  error: string | null
}
