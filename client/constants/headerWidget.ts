/**
 * Where the shared header widget is loaded from, and the cache-bust token its address carries.
 *
 * @module constants/headerWidget
 */

/** Path of the widget script on this portal's own origin, relayed from the shell by `server/routes/widget`. */
export const HEADER_WIDGET_PATH = '/widget/header.js'

/**
 * One-time cache-bust token shared with the other products. Do not bump it per widget release:
 * the main portal once served the script behind `expires 1y` + `immutable`, and this token is
 * what reaches browsers still holding that entry.
 */
export const HEADER_WIDGET_CACHE_BUST = '20260918'
