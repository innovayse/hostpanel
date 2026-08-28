/** The fields `POST /api/contact` needs to relay a message from the public contact form. */
export interface ContactMessage {
  /** Sender's name. */
  name: string
  /** Sender's email — where the reply goes. */
  email: string
  /** Sender's phone number, when they gave one. */
  phone?: string
  /** Which service the enquiry is about, when they picked one. */
  service?: string
  /** The message body. */
  message: string
  /** Locally formatted send time, included in the relayed mail for context. */
  timestamp?: string
}
