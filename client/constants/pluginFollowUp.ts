/**
 * The bounds of a plugin view's follow-up refresh (`followUp` of an action or a form). They belong to the host, never
 * to the plugin: a view names what to watch, and the host decides how often and how long it reads, so no plugin can
 * make a reader's panel poll faster or forever. Twenty attempts three seconds apart watch for about a minute.
 *
 * @module constants/pluginFollowUp
 */

/** The most route reads one follow-up makes before it gives up. */
export const PLUGIN_FOLLOW_UP_MAX_ATTEMPTS = 20

/** The time between two follow-up reads, in milliseconds; the first read waits this long after the call too. */
export const PLUGIN_FOLLOW_UP_INTERVAL_MS = 3000
