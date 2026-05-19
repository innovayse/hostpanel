/** Status badge styles for client accounts. */
export const CLIENT_STATUS_STYLES: Record<string, string> = {
  Active: 'text-status-green bg-status-green/10 border-status-green/20',
  Suspended: 'text-status-yellow bg-status-yellow/10 border-status-yellow/20',
  Closed: 'text-status-red bg-status-red/10 border-status-red/20',
  Inactive: 'text-text-muted bg-white/[0.04] border-border',
}

/** Status badge styles for services. */
export const SERVICE_STATUS_STYLES: Record<string, string> = {
  Active: 'text-status-green bg-status-green/10 border-status-green/20',
  Pending: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  Suspended: 'text-status-yellow bg-status-yellow/10 border-status-yellow/20',
  Terminated: 'text-text-muted bg-white/[0.04] border-border',
}

/** Contact type badge styles. */
export const CONTACT_TYPE_STYLES: Record<string, string> = {
  Billing: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  Technical: 'text-status-yellow bg-status-yellow/10 border-status-yellow/20',
  General: 'text-text-secondary bg-white/[0.04] border-border',
}

/** Client account status options for dropdowns. */
export const CLIENT_STATUS_OPTIONS = [
  { value: 'Active', label: 'Active' },
  { value: 'Inactive', label: 'Inactive' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Closed', label: 'Closed' },
]

/** Service status options for dropdowns. */
export const SERVICE_STATUS_OPTIONS = [
  { value: 'Pending', label: 'Pending' },
  { value: 'Active', label: 'Active' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Terminated', label: 'Terminated' },
]

/** Language options for dropdowns. */
export const LANGUAGE_OPTIONS = [
  { value: '', label: 'Default' },
  { value: 'en', label: 'English' },
  { value: 'ru', label: 'Russian' },
  { value: 'hy', label: 'Armenian' },
]

/** Status badge styles for domains. */
export const DOMAIN_STATUS_STYLES: Record<string, string> = {
  Active: 'text-status-green bg-status-green/10 border-status-green/20',
  PendingRegistration: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  PendingTransfer: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  Expired: 'text-status-red bg-status-red/10 border-status-red/20',
  Redemption: 'text-status-yellow bg-status-yellow/10 border-status-yellow/20',
  Transferred: 'text-text-muted bg-white/[0.04] border-border',
  Cancelled: 'text-text-muted bg-white/[0.04] border-border',
}

/** Domain status options for dropdowns. */
export const DOMAIN_STATUS_OPTIONS = [
  { value: 'Active', label: 'Active' },
  { value: 'PendingRegistration', label: 'Pending Registration' },
  { value: 'PendingTransfer', label: 'Pending Transfer' },
  { value: 'Expired', label: 'Expired' },
  { value: 'Redemption', label: 'Redemption' },
  { value: 'Transferred', label: 'Transferred' },
  { value: 'Cancelled', label: 'Cancelled' },
]

/** Payment method options for dropdowns. */
export const PAYMENT_METHOD_OPTIONS = [
  { value: '', label: 'None' },
  { value: 'Credit/Debit Card', label: 'Credit/Debit Card' },
  { value: 'Bank Transfer', label: 'Bank Transfer' },
  { value: 'PayPal', label: 'PayPal' },
]

/** DNS record type options for dropdowns. */
export const DNS_RECORD_TYPE_OPTIONS = [
  { value: 'A', label: 'A' },
  { value: 'AAAA', label: 'AAAA' },
  { value: 'CNAME', label: 'CNAME' },
  { value: 'MX', label: 'MX' },
  { value: 'TXT', label: 'TXT' },
  { value: 'NS', label: 'NS' },
  { value: 'SRV', label: 'SRV' },
]

/** Billing cycle options for service dropdowns. */
export const BILLING_CYCLE_OPTIONS = [
  { value: 'free', label: 'Free' },
  { value: 'monthly', label: 'Monthly' },
  { value: 'quarterly', label: 'Quarterly' },
  { value: 'semi-annual', label: 'Semi-Annual' },
  { value: 'annual', label: 'Annual' },
  { value: 'biennial', label: 'Biennial' },
  { value: 'triennial', label: 'Triennial' },
]

/** Invoice action options for billable item forms. */
export const INVOICE_ACTION_OPTIONS = [
  { value: 'DontInvoice', label: "Don't Invoice for Now" },
  { value: 'InvoiceOnNextCron', label: 'Invoice on Next Cron Run' },
  { value: 'AddToNextInvoice', label: "Add to User's Next Invoice" },
  { value: 'InvoiceForDueDate', label: 'Invoice as Normal for Due Date' },
  { value: 'Recur', label: 'Recur' },
]

/** Recurrence period options for billable items. */
export const RECURRENCE_PERIOD_OPTIONS = [
  { value: 'Days', label: 'Days' },
  { value: 'Weeks', label: 'Weeks' },
  { value: 'Months', label: 'Months' },
  { value: 'Years', label: 'Years' },
]

/** Invoice status options for filter dropdowns. */
export const INVOICE_STATUS_OPTIONS = [
  { value: '', label: 'All Statuses' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Unpaid', label: 'Unpaid' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Refunded', label: 'Refunded' },
]

/** Invoice status badge styles. */
export const INVOICE_STATUS_STYLES: Record<string, string> = {
  Draft: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  Unpaid: 'text-status-yellow bg-status-yellow/10 border-status-yellow/20',
  Paid: 'text-status-green bg-status-green/10 border-status-green/20',
  Overdue: 'text-status-red bg-status-red/10 border-status-red/20',
  Cancelled: 'text-text-muted bg-white/[0.04] border-border',
  Refunded: 'text-purple-400 bg-purple-500/10 border-purple-500/20',
}

/** Payment gateway options. */
export const GATEWAY_OPTIONS = [
  { value: 'None', label: 'None' },
  { value: 'BankTransfer', label: 'Bank Transfer' },
  { value: 'Stripe', label: 'Stripe' },
  { value: 'PayPal', label: 'PayPal' },
]
