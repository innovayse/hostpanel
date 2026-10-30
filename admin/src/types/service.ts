/**
 * A provisioned service as the list and the detail page read it, plus a pending cancellation against one.
 */


/** Enriched service DTO for admin service list views. */
export interface ServiceListItem {
  /** Unique service identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client; every service has one, since its client cannot be deleted. */
  clientName: string
  /** Name of the ordered product; every service has one, since its product cannot be deleted. */
  productName: string
  /** Linked domain name, or null if no domain is linked. */
  domain: string | null
  /** Resolved price based on billing cycle. */
  price: number
  /** ISO 4217 currency code (e.g. "USD"). */
  priceCurrency: string
  /** Billing cycle. */
  billingCycle: BillingCycle
  /** Current lifecycle status. */
  status: ServiceStatus
  /** ISO 8601 next renewal date, or null if not yet active. */
  nextDueDate: string | null
}

/** The provisioning server a service is hosted on, as far as it could be resolved. */
export interface ServiceServer {
  /** Display name of the server. */
  name: string
  /** Hostname of the server. */
  hostname: string
  /** IP address of the server, or null when it has none. */
  ipAddress: string | null
}

/** The hosting account a service was provisioned as. */
export interface ServiceAccount {
  /** Dedicated IP address, if assigned. */
  dedicatedIp: string | null
  /** Control panel or service username. */
  username: string | null
  /** Control panel or service password. */
  password: string | null
  /** External provisioning reference (the hosting panel's account id). */
  provisioningRef: string | null
}

/** What a service costs, how often and how it is paid for. */
export interface ServiceBillingTerms {
  /** Quantity of units purchased. */
  quantity: number
  /** Amount charged for the first payment. */
  firstPaymentAmount: number
  /** Recurring charge per billing cycle. */
  recurringAmount: number
  /** Payment method identifier or label. */
  paymentMethod: string | null
  /** Applied promotion code, if any. */
  promotionCode: string | null
  /** External subscription ID at the payment gateway. */
  subscriptionId: string | null
  /** Billing cycle. */
  billingCycle: BillingCycle
  /** ISO 8601 next renewal date, if applicable. */
  nextDueDate: string | null
  /** Whether the billing runs look after the service; false means staff bill it by hand. */
  autoRenew: boolean
}

/** The automatic suspension and termination settings of a service. */
export interface ServiceAutomation {
  /** Whether automatic suspension is overridden. */
  overrideAutoSuspend: boolean
  /** ISO 8601 date until which the service is suspended. */
  suspendUntil: string | null
  /** Whether the service auto-terminates at end of billing cycle. */
  autoTerminateEndOfCycle: boolean
  /** Reason for auto-termination, if applicable. */
  autoTerminateReason: string | null
}

/** Full service detail returned by GET /api/services/{id}. */
export interface ServiceDetail {
  /** Unique service identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client; every service has one, since its client cannot be deleted. */
  clientName: string
  /** Product identifier. */
  productId: number
  /** Name of the ordered product; every service has one, since its product cannot be deleted. */
  productName: string
  /** Linked domain name, if any. */
  domain: string | null
  /** Current lifecycle status. */
  status: ServiceStatus
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** ISO 8601 termination timestamp, if terminated. */
  terminatedAt: string | null
  /** Internal admin notes. */
  adminNotes: string | null
  /** FK to the assigned server, if any. */
  serverId: number | null
  /** The assigned server; null when none is assigned or it could not be resolved. */
  server: ServiceServer | null
  /** The hosting account: dedicated IP, credentials and provisioning reference. */
  account: ServiceAccount
  /** Quantity, prices, payment details, cycle and renewal. */
  billing: ServiceBillingTerms
  /** The automatic suspension and termination settings. */
  automation: ServiceAutomation
}

/** DTO for a cancellation request in the admin list. */
export interface CancellationRequestItem {
  /** Unique identifier. */
  id: number
  /** ID of the service being cancelled. */
  serviceId: number
  /** Product name of the service; every request has one, since its service's product cannot be deleted. */
  serviceName: string
  /** ID of the owning client. */
  clientId: number
  /** Full name of the owning client; every request has one, since its service's client cannot be deleted. */
  clientName: string
  /** How the cancellation ends the service. */
  type: CancellationType
  /** Reason provided by the client. */
  reason: string | null
  /** Where the request stands. */
  status: CancellationStatus
  /** ISO 8601 creation date. */
  createdAt: string
}

/** Filter and paging of the service list. */
export interface ServiceFilter {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
  /** Keep only this client's services. */
  clientId?: number | string
}

/** Body of the billing section save: what the service is billed. */
export interface ServiceBillingTermsPayload {
  /** Product the service is of. */
  productId: number
  /** Quantity; at least one. */
  quantity: number
  /** Billing cycle. */
  billingCycle: BillingCycle
  /** First payment amount. */
  firstPaymentAmount: number
  /** Recurring amount. */
  recurringAmount: number
  /** ISO 8601 next renewal date, or null to clear it. */
  nextRenewalAt: string | null
  /** Payment method label, or null. */
  paymentMethod: string | null
  /** Applied promotion code, or null. */
  promotionCode: string | null
  /** External subscription reference, or null. */
  subscriptionId: string | null
}

/** Body of the hosting account section save: where and under which account the service is hosted. */
export interface ServiceHostingAccountPayload {
  /** Server the service runs on, or null for none. */
  serverId: number | null
  /** Primary domain, or null. */
  domain: string | null
  /** Reference on the provisioning server, or null. */
  provisioningRef: string | null
  /** Dedicated IP address, or null. */
  dedicatedIp: string | null
  /** Hosting account username, or null. */
  username: string | null
  /** Hosting account password, or null. */
  password: string | null
}

/** Body of the automation section save: manual or automatic billing, the suspension hold and the end-of-cycle termination. */
export interface ServiceAutomationPayload {
  /** Whether the billing runs look after the service; false means staff bill it by hand. */
  autoRenew: boolean
  /** Whether the overdue suspension is held off. */
  overrideAutoSuspend: boolean
  /** ISO 8601 date the hold lasts until; null without the hold. */
  suspendUntil: string | null
  /** Whether the service terminates at the end of the cycle. */
  autoTerminateEndOfCycle: boolean
  /** Reason recorded for the termination; null without it. */
  autoTerminateReason: string | null
}

/** Body of the notes section save. */
export interface ServiceNotesPayload {
  /** Free-text admin notes, or null to clear them. */
  adminNotes: string | null
}

/** Body of a status change. */
export interface ServiceStatusPayload {
  /** The status to move the service to. */
  status: ServiceStatus
}

/** Body of a termination date correction. */
export interface ServiceTerminationDatePayload {
  /** ISO 8601 date the service really ended. */
  terminatedAt: string
}

/** Body of the suspend and terminate provisioning commands. */
export interface ProvisioningReasonPayload {
  /** Reason recorded with the command. */
  reason: string
}

/** Reasons the admin records when it suspends or terminates a service from the service page. */
export enum ServiceChangeReason {
  /** Recorded on a suspension. */
  Suspended = 'Admin suspended',
  /** Recorded on a termination. */
  Terminated = 'Admin terminated',
}

/** Status of a hosted service, exactly as the API sends it. */
export enum ServiceStatus {
  Active = 'Active',
  Pending = 'Pending',
  Suspended = 'Suspended',
  Terminated = 'Terminated',
}

/** Billing cycles a service can renew on, exactly as the API sends them. */
export enum BillingCycle {
  /** Every month. */
  Monthly = 'Monthly',
  /** Every year. */
  Annual = 'Annual',
}

/** How a cancellation request ends the service, exactly as the API sends it. */
export enum CancellationType {
  /** The service stops at once. */
  Immediate = 'Immediate',
  /** The service runs until the end of the paid period. */
  EndOfBillingPeriod = 'EndOfBillingPeriod',
}

/** Where a cancellation request stands, exactly as the API sends it. */
export enum CancellationStatus {
  /** Waiting for the operator. */
  Open = 'Open',
  /** Handled. */
  Closed = 'Closed',
}

/** Path segment of each separately saved section of a service, under `/services/{id}/`. */
export enum ServiceSection {
  /** What the service is billed. */
  Billing = 'billing',
  /** Where and under which account it is hosted. */
  HostingAccount = 'hosting-account',
  /** Suspension hold and end-of-cycle termination. */
  Automation = 'automation',
  /** Admin notes. */
  Notes = 'notes',
  /** Lifecycle status. */
  Status = 'status',
  /** Termination date correction. */
  TerminationDate = 'termination-date',
}

/** Body of `POST /api/services/existing`: a service for an account that already exists on a server. */
export interface AddExistingServicePayload {
  /** Client the service belongs to. */
  clientId: number
  /** Product the service is of. */
  productId: number
  /** Billing cycle. */
  billingCycle: string
  /** Amount each renewal invoice charges. */
  recurringAmount: number
  /** Server the account lives on. */
  serverId: number
  /** The account's primary domain. */
  domain: string
  /** The account's username on the panel. */
  username: string
  /** When the current billing period began (ISO date), or null for today. */
  startedAt: string | null
}
