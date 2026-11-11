/** The panels of the service detail page. */
export enum ServiceTab {
  /** Details, server, configuration and invoices. */
  Overview = 'overview',
  /** Control-panel password form. */
  Password = 'password',
  /** Move to another plan. */
  PlanChange = 'planChange',
  /** Cancellation request form. */
  Cancel = 'cancel',
  /** SSH key management. */
  Ssh = 'ssh'
}
