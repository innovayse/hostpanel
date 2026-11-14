/**
 * The choices of an enabled-state filter on a list whose rows are switched on or off (TLD configurations, tax rules).
 * "Any" is the empty value of the dropdown, so it is not a member.
 */

/** Which rows a list shows by their enabled state. */
export enum EnabledFilter {
  /** Only the rows that are switched on. */
  Enabled = 'enabled',
  /** Only the rows that are switched off. */
  Disabled = 'disabled',
}
