/** One row of the plan comparison table: a label and its value per plan, in plan order. */
export interface ComparisonRow {
  /** The specification line's name, e.g. "Disk space". */
  label: string
  /** Values aligned to the plan ids passed in; an empty string where a plan has no such line. */
  values: string[]
}
