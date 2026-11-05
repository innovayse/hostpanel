/**
 * A plugin's own failure shown inside a chat reply.
 *
 * @module types/pluginchatissue
 */

/** One issue. */
export interface PluginChatIssue {
  /** A key unique within the chat. */
  key: number
  /** The plugin's sentence, rendered as text. */
  text: string
}
