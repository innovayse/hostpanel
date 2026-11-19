/**
 * Vocabulary of the report schema: the header buttons a report offers and the note it can end with.
 *
 * `ReportPage` draws both from the schema of the report, and the skeleton of the report counts and draws the same
 * parts, so the closed sets live here once, with no component.
 */

/** Closed set of the buttons a report shows in its header, in the order a schema lists them. */
export const ReportAction = {
  /** Loads the report again. */
  Refresh: 'refresh',
  /** Downloads the report as a CSV file. */
  Export: 'export',
  /** Opens the print dialog. */
  Print: 'print',
} as const

/** One of the header buttons. */
export type ReportAction = (typeof ReportAction)[keyof typeof ReportAction]

/** Closed set of the notes a report can end with, which name the currency the figures are in. */
export const ReportNote = {
  /** The figures were converted to the base currency. */
  Converted: 'converted',
  /** The recurring figures are in the base currency. */
  Recurring: 'recurring',
} as const

/** One of the notes. */
export type ReportNote = (typeof ReportNote)[keyof typeof ReportNote]
