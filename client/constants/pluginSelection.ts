/**
 * The contract of a preview frame's selection messages and its bounds. The plugin's server and the service it calls hold
 * the same bounds; these keep a forged flood from reaching them.
 *
 * @module constants/pluginSelection
 */

/** The `type` of every message, both ways. */
export const PLUGIN_SELECTION_MESSAGE_TYPE = 'hostpanel.preview.selection'

/** The `version` of every message; another one is refused whole. */
export const PLUGIN_SELECTION_MESSAGE_VERSION = 1

/** The most elements one selection may hold. */
export const PLUGIN_SELECTION_MAX_IDS = 20

/** The longest element id, in characters. */
export const PLUGIN_SELECTION_MAX_ID_LENGTH = 128

/** The form of an element id: a dotted path of names and indexes. */
export const PLUGIN_SELECTION_ID_PATTERN = /^[a-z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+){0,15}$/
