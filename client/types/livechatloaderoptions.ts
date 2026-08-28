/** Everything the live-chat loader script needs to start the widget. */
export interface LiveChatLoaderOptions {
  /** Origin of the chat host, without a trailing slash. */
  baseUrl: string
  /** Website token identifying this site to the chat host. */
  websiteToken: string
  /** Locale code to hand the widget once it is ready. */
  locale: string
  /** Language name recorded on the conversation. */
  language: string
}
