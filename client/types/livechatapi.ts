/** The subset of the live-chat widget's API this application calls. */
export interface LiveChatApi {
  /** Opens or closes the chat panel. */
  toggle: (state: 'open' | 'close') => void
  /** Switches the widget's own UI language. */
  setLocale: (locale: string) => void
  /** Records attributes on the conversation, so an agent sees them. */
  setCustomAttributes: (attributes: Record<string, string>) => void
}
