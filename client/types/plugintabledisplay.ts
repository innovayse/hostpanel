/**
 * How a table block draws its rows.
 *
 * @module types/plugintabledisplay
 */

/** The ways a table block draws its rows. */
export enum PluginTableDisplay {
  /** A table with a row per item, the default. */
  Table = 'table',
  /** One card per item, for a narrow pane: the first column as the title, the others under it. */
  Cards = 'cards',
}
