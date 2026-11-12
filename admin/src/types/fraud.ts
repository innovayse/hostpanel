/**
 * The fraud-detection area: the closed value sets the API sends (block list types, check kinds,
 * decisions, reason codes), the rows of the block list and of the fraud log, the fraud settings,
 * and the filters and payloads the screens send. The members of every enum are the names the API
 * puts on the wire.
 */

/** What a block list entry matches. */
export enum BlockListType {
  /** One IP address. */
  Ip = 'Ip',
  /** An IP range written in CIDR notation. */
  Cidr = 'Cidr',
  /** The domain part of an email address. */
  EmailDomain = 'EmailDomain',
  /** A country, by its ISO 3166 alpha-2 code. */
  Country = 'Country',
}

/** The moment a fraud check ran at. */
export enum FraudCheckKind {
  /** A new account was registered. */
  Registration = 'Registration',
  /** An order was placed. */
  Order = 'Order',
}

/** How a fraud check ended. */
export enum FraudDecision {
  /** The risk was low; the action went through. */
  Approved = 'Approved',
  /** The risk was medium; the order waits for staff. */
  Review = 'Review',
  /** The risk was high; the action was refused. */
  Rejected = 'Rejected',
  /** Staff approved an order that waited for review. */
  ManualApproved = 'ManualApproved',
  /** Staff rejected an order that waited for review. */
  ManualRejected = 'ManualRejected',
}

/** Why a fraud check raised the risk. */
export enum FraudReasonCode {
  /** The address is on the block list. */
  BlockedIp = 'BlockedIp',
  /** The email domain is on the block list. */
  BlockedEmailDomain = 'BlockedEmailDomain',
  /** The country is on the block list. */
  BlockedCountry = 'BlockedCountry',
  /** The email domain is a disposable mailbox service. */
  DisposableEmail = 'DisposableEmail',
  /** Too many accounts were made from the address. */
  SharedIpAccounts = 'SharedIpAccounts',
  /** Too many orders in a short time. */
  OrderVelocity = 'OrderVelocity',
  /** The address country differs from the billing country. */
  CountryMismatch = 'CountryMismatch',
  /** The risk scoring provider rated the action high. */
  ProviderHighRisk = 'ProviderHighRisk',
  /** The address is a proxy or an anonymising network. */
  AnonymousIp = 'AnonymousIp',
}

/** One entry of a block list. */
export interface BlockListEntry {
  /** Entry id. */
  id: number
  /** What the value is. */
  type: BlockListType
  /** The blocked value, normalised by the API. */
  value: string
  /** Why it was blocked, when a note was written. */
  note: string | null
  /** ISO 8601 time the entry was added. */
  createdAt: string
}

/** What the screen sends to add a block list entry. */
export interface AddBlockListEntryPayload {
  /** What the value is. */
  type: BlockListType
  /** The value to block. */
  value: string
  /** Optional reason, up to 500 characters; null for none. */
  note: string | null
}

/** One recorded fraud check. */
export interface FraudLogEntry {
  /** Entry id. */
  id: number
  /** When the check ran. */
  kind: FraudCheckKind
  /** How it ended. */
  decision: FraudDecision
  /** The client the check was about, when it had one. */
  clientId: number | null
  /** The order the check was about, when it had one. */
  orderId: number | null
  /** The visitor's address, when known. */
  ipAddress: string | null
  /** The email address checked, when known. */
  email: string | null
  /** Risk score, 0 to 100. */
  score: number
  /** Why the risk was raised; empty for a clean check. */
  reasons: FraudReasonCode[]
  /** ISO 8601 time the check ran. */
  createdAt: string
}

/** Filters the fraud log can be listed by; an empty value means no filter. */
export interface FraudLogFilters {
  /** Decision, or empty for any. */
  decision: FraudDecision | ''
  /** Reason code, or empty for any. */
  reason: FraudReasonCode | ''
}

/** The fraud detection settings. */
export interface FraudSettings {
  /** Whether fraud checks run at all. */
  enabled: boolean
  /** Score from which an order waits for staff review. */
  manualReviewRiskScore: number
  /** Score from which an action is refused at once. */
  autoRejectRiskScore: number
  /** Length of the order-velocity window, in minutes. */
  velocityWindowMinutes: number
  /** Orders allowed inside the velocity window. */
  velocityMaxOrders: number
  /** Accounts allowed from one address. */
  maxAccountsPerIp: number
}

/** The numeric settings fields, named as the API names them (the key of a field error). */
export enum FraudSettingsField {
  /** {@link FraudSettings.manualReviewRiskScore}. */
  ManualReviewRiskScore = 'manualReviewRiskScore',
  /** {@link FraudSettings.autoRejectRiskScore}. */
  AutoRejectRiskScore = 'autoRejectRiskScore',
  /** {@link FraudSettings.velocityWindowMinutes}. */
  VelocityWindowMinutes = 'velocityWindowMinutes',
  /** {@link FraudSettings.velocityMaxOrders}. */
  VelocityMaxOrders = 'velocityMaxOrders',
  /** {@link FraudSettings.maxAccountsPerIp}. */
  MaxAccountsPerIp = 'maxAccountsPerIp',
}
