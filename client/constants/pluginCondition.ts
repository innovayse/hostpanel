/**
 * The bounds of a condition a plugin writes into a chat event (a result card action's `visibleWhen` and `until`). A view
 * document's conditions are checked at install with the same limits; an event is not, so the panel checks it when it
 * reads it, and the numbers are written once here.
 *
 * @module constants/pluginCondition
 */

/** The most values one condition may list; the API's cap on a view condition. */
export const PLUGIN_CONDITION_MAX_VALUES = 20

/** The longest dotted field path a condition may name, in characters; the API's cap on a view condition. */
export const PLUGIN_CONDITION_MAX_FIELD_LENGTH = 200

/** A dotted field path: segments of letters, digits, `_` and `-` joined by single dots. */
export const PLUGIN_CONDITION_FIELD_PATTERN = /^[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)*$/
