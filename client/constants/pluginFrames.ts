/**
 * What the panel's server and the preview block agree on about a plugin page's document.
 *
 * A CSP header cannot be read from script, and a document served to a signed-out visitor keeps its closed `frame-src`
 * for as long as it stays open: a sign-in inside it moves the router, not the document. So the document says what it
 * got in a `Server-Timing` entry, the one response header of a navigation script may read.
 *
 * @module constants/pluginFrames
 */

/** The `Server-Timing` entry a document carries when its `frame-src` holds the origins the API answered. */
export const PLUGIN_FRAME_TIMING = 'plugin-frames'

/**
 * The query parameter that makes a redrawn frame's address new to the browser, so its cache cannot answer with the old page.
 * Reserved for the host: the name is prefixed so a plugin page's own query parameters never collide with it, and the
 * plugin developer guide says a page must not use it.
 */
export const PLUGIN_FRAME_REVISION_PARAM = 'hp-revision'
