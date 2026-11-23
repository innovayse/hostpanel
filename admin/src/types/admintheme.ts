/**
 * The colour schemes an operator can switch the admin panel between.
 */

/** The operator's theme choice, as stored in the browser. Never choosing means following the operating system. */
export enum AdminTheme {
  /** Always the light palette. */
  Light = 'light',
  /** Always the dark palette. */
  Dark = 'dark',
}
