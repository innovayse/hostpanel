/**
 * The age limit of the picture a plugin view shares between its readers (`readPluginRoute` in `usePluginView`). It
 * belongs to the host, never to the plugin: a view that stays open for a long time must not answer a block that mounts
 * much later from data read when the page opened, whatever the page did or did not refresh in between.
 *
 * @module constants/pluginViewReads
 */

/**
 * How long, in milliseconds, one picture of a view's reads may answer new readers. Readers that mount together are served
 * within a moment, and a follow-up polls every few seconds, so ten seconds shares a page load and its refreshes without
 * keeping data a reader would notice as old.
 */
export const PLUGIN_VIEW_READ_MAX_AGE_MS: number = 10_000
