import { UiTone } from '@/utils/ui/tone'

/** The expiry groups of the SSL monitoring report; each value is the stable code the API sends, worded here through the locale keys below. */
export enum SslGroupKind {
  Within30Days = 'Within30Days',
  Within90Days = 'Within90Days',
  Within180Days = 'Within180Days',
  Over180Days = 'Over180Days',
  NoSsl = 'NoSsl',
}

/** Locale key of the title shown for each expiry group. */
export const SSL_GROUP_TITLE_KEYS: Record<SslGroupKind, string> = {
  [SslGroupKind.Within30Days]: 'reports.sslMonitoring.groups.within30Days',
  [SslGroupKind.Within90Days]: 'reports.sslMonitoring.groups.within90Days',
  [SslGroupKind.Within180Days]: 'reports.sslMonitoring.groups.within180Days',
  [SslGroupKind.Over180Days]: 'reports.sslMonitoring.groups.over180Days',
  [SslGroupKind.NoSsl]: 'reports.sslMonitoring.groups.noSsl',
}

/** Tone of the summary chip of each expiry group: the sooner the expiry, the louder. */
export const SSL_GROUP_TONES: Record<SslGroupKind, UiTone> = {
  [SslGroupKind.Within30Days]: UiTone.Danger,
  [SslGroupKind.Within90Days]: UiTone.Warning,
  [SslGroupKind.Within180Days]: UiTone.Info,
  [SslGroupKind.Over180Days]: UiTone.Success,
  [SslGroupKind.NoSsl]: UiTone.Neutral,
}

/** SSL status of one domain. */
export interface ReportSslRow {
  /** The domain. */
  domainName: string
  /** Whether a certificate was found. */
  hasSsl: boolean
  /** Certificate issuer, when known. */
  issuer: string | null
  /** Certificate expiry, when known. */
  expiresAt: string | null
  /** When the status was last checked. */
  lastUpdate: string
  /** Whether the domain is active. */
  isActive: boolean
}

/** The domains of one group. */
export interface ReportSslGroup {
  /** Which expiry group this is. */
  group: SslGroupKind
  /** Domains of the group. */
  rows: ReportSslRow[]
}

/** The SSL monitoring report. */
export interface ReportSsl {
  /** Domain groups. */
  groups: ReportSslGroup[]
}
