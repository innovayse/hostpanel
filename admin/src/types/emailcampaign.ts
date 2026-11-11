/**
 * Email campaigns: the campaign records the API returns, the payloads that save, schedule and count
 * them, and the closed value sets the API speaks for them (status, segment filter).
 */

/** Lifecycle status of a campaign. */
export enum CampaignStatus {
  /** Being written; can still be edited, deleted, scheduled or sent. */
  Draft = 'Draft',
  /** Waiting for its send time; the schedule can be cancelled. */
  Scheduled = 'Scheduled',
  /** Being delivered to its recipients. */
  Sending = 'Sending',
  /** Delivered to every recipient. */
  Sent = 'Sent',
}

/** The kind of rule one audience segment uses to pick clients. */
export enum CampaignFilterType {
  /** Every active client; takes no value. */
  AllActive = 'AllActive',
  /** Clients with a service of one product; the value is the product id. */
  Product = 'Product',
  /** Clients in one country; the value is the ISO 3166 two-letter code. */
  Country = 'Country',
  /** Clients with a service expiring within a number of days; the value is 1 to 365. */
  ServiceExpiringInDays = 'ServiceExpiringInDays',
  /** Clients who belong to one client group; the value is the group id. */
  ClientGroup = 'ClientGroup',
  /** Clients who have not signed in for a number of days; the value is 1 to 3650. */
  NotSignedInForDays = 'NotSignedInForDays',
}

/** The range of days a days-based audience rule accepts, both ends included. */
export interface CampaignDaysRange {
  /** Fewest days the rule accepts. */
  min: number
  /** Most days the rule accepts. */
  max: number
}

/** One rule that picks the clients of a campaign; a client matched by any segment is a recipient. */
export interface EmailCampaignSegment {
  /** Kind of rule. */
  filterType: CampaignFilterType
  /** What the rule needs (product id, country code, client group id or days as text); null for `AllActive`. */
  filterValue: string | null
}

/** Delivery counters of a campaign. */
export interface EmailCampaignStats {
  /** Recipients in total. */
  total: number
  /** Recipients still waiting for their mail. */
  pending: number
  /** Recipients the mail was handed over to. */
  sent: number
  /** Recipients the mail could not be delivered to. */
  failed: number
  /** Recipients who unsubscribed. */
  unsubscribed: number
  /** Every load of a mail's tracking pixel, repeated opens by one reader included. */
  opened: number
  /** Delivered recipients who opened their mail at least once. */
  uniqueOpened: number
  /** Every follow of a tracked link, repeated clicks by one reader included. */
  clicked: number
  /** Delivered recipients who followed a tracked link at least once. */
  uniqueClicked: number
  /** Share of the delivered mails opened at least once, from 0 to 1. */
  openRate: number
  /** Share of the delivered mails with at least one clicked link, from 0 to 1. */
  clickRate: number
}

/** A campaign as the list endpoint returns it. */
export interface EmailCampaignListItem {
  /** Campaign primary key. */
  id: number
  /** Mail subject. */
  subject: string
  /** Lifecycle status. */
  status: CampaignStatus
  /** ISO 8601 send time of a scheduled campaign, null when none is set. */
  scheduledAt: string | null
  /** ISO 8601 moment the campaign finished sending, null while it has not. */
  sentAt: string | null
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** Delivery counters. */
  stats: EmailCampaignStats
}

/** A campaign with its body and audience, as the detail endpoint returns it. */
export interface EmailCampaign extends EmailCampaignListItem {
  /** HTML body of the mail. */
  body: string
  /** Who created the campaign. */
  createdBy: string
  /** The audience rules. */
  segments: EmailCampaignSegment[]
}

/** Body that creates or replaces a draft campaign, and the one the audience count reads the segments of. */
export interface EmailCampaignPayload {
  /** Mail subject. */
  subject: string
  /** HTML body of the mail. */
  body: string
  /** The audience rules. */
  segments: EmailCampaignSegment[]
}

/** The campaign mail as the recipients would see it. */
export interface EmailCampaignPreview {
  /** Rendered subject. */
  subject: string
  /** Rendered HTML, with a sample unsubscribe link. */
  html: string
}

/** The audience size the API counted for a set of segments. */
export interface EmailCampaignAudience {
  /** Number of distinct clients the segments match. */
  count: number
}

/** One audience rule as the form edits it: the wire fields plus a key that stays put while rows move. */
export interface EmailCampaignSegmentDraft {
  /** Stable key of the row, for the list rendering. */
  key: number
  /** Kind of rule. */
  filterType: CampaignFilterType
  /** The value as typed or picked; empty text for a rule that takes none. */
  filterValue: string
}
