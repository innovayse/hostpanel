/** All supported control panel module identifiers. */
export type ServerModule =
  | 'CPanel'
  | 'Plesk'
  | 'DirectAdmin'
  | 'Cwp7'
  | 'Lukittu'
  | 'InnovayseWordPressHosting'
  | 'Virtualmin'
  | 'HyperVm'

/** Account distribution strategy for a server group. */
export type ServerFillType = 'LeastFull' | 'FillUntilFull'

/** DTO for a single provisioning server. */
export interface ServerDto {
  /** Unique server identifier. */
  id: number
  /** Display name. */
  name: string
  /** Hostname or IP used for API access. */
  hostname: string
  /** Primary public IP address, or null. */
  ipAddress: string | null
  /** Additional assigned IPs (newline-separated), or null. */
  assignedIpAddresses: string | null
  /** Control panel module type string. */
  module: ServerModule
  /** API username. */
  username: string
  /** Whether SSL is used for API connections. */
  useSSL: boolean
  /** Account capacity limit, or null for unlimited. */
  maxAccounts: number | null
  /** Whether this is the default server for the module. */
  isDefault: boolean
  /** Whether this server is disabled. */
  isDisabled: boolean
  /** Monthly cost in USD. */
  monthlyCost: number
  /** Datacenter or NOC provider name, or null. */
  datacenter: string | null
  /** Status page URL, or null. */
  serverStatusAddress: string | null
  /** Primary nameserver hostname. */
  ns1Hostname: string | null
  /** Primary nameserver IP. */
  ns1Ip: string | null
  /** Secondary nameserver hostname. */
  ns2Hostname: string | null
  /** Secondary nameserver IP. */
  ns2Ip: string | null
  /** Third nameserver hostname. */
  ns3Hostname: string | null
  /** Third nameserver IP. */
  ns3Ip: string | null
  /** Fourth nameserver hostname. */
  ns4Hostname: string | null
  /** Fourth nameserver IP. */
  ns4Ip: string | null
  /** Fifth nameserver hostname. */
  ns5Hostname: string | null
  /** Fifth nameserver IP. */
  ns5Ip: string | null
  /** ID of the assigned group, or null. */
  serverGroupId: number | null
  /** Name of the assigned group, or null. */
  serverGroupName: string | null
  /** UTC creation timestamp. */
  createdAt: string
  /** Whether the last connection test succeeded, or null if never tested. */
  isOnline: boolean | null
  /** UTC timestamp of the last connection test, or null if never tested. */
  lastTestedAt: string | null
  /** Account count from the last test, or null if unknown. */
  accountsCount: number | null
}

/** Result of a server connection test. */
export interface TestConnectionResultDto {
  /** Whether the connection succeeded. */
  connected: boolean
  /** Total accounts on the server, or null if unavailable. */
  accountsCount: number | null
  /** Server software version, or null. */
  version: string | null
  /** Error message if connection failed, or null. */
  errorMessage: string | null
  /** UTC timestamp of the test. */
  testedAt: string
}

/** DTO for a server group including its assigned servers. */
export interface ServerGroupDto {
  /** Unique group identifier. */
  id: number
  /** Display name. */
  name: string
  /** Fill type string. */
  fillType: ServerFillType
  /** Servers assigned to this group. */
  servers: ServerDto[]
  /** UTC creation timestamp. */
  createdAt: string
}

/** Payload for creating or updating a server. */
export interface ServerPayload {
  name: string
  hostname: string
  ipAddress: string | null
  assignedIpAddresses: string | null
  module: ServerModule
  username: string
  password: string | null
  apiToken: string | null
  accessHash: string | null
  useSSL: boolean
  maxAccounts: number | null
  isDefault: boolean
  isDisabled: boolean
  monthlyCost: number
  datacenter: string | null
  serverStatusAddress: string | null
  ns1Hostname: string | null
  ns1Ip: string | null
  ns2Hostname: string | null
  ns2Ip: string | null
  ns3Hostname: string | null
  ns3Ip: string | null
  ns4Hostname: string | null
  ns4Ip: string | null
  ns5Hostname: string | null
  ns5Ip: string | null
}

/** Payload for creating or updating a server group. */
export interface ServerGroupPayload {
  /** Display name. */
  name: string
  /** Fill strategy. */
  fillType: ServerFillType
  /** IDs of servers assigned to this group. */
  serverIds: number[]
}

/** Human-readable labels for each module. */
export const MODULE_LABELS: Record<ServerModule, string> = {
  CPanel: 'cPanel / WHM',
  Plesk: 'Plesk',
  DirectAdmin: 'DirectAdmin',
  Cwp7: 'CentOS Web Panel',
  Lukittu: 'Lukittu',
  InnovayseWordPressHosting: 'Innovayse WordPress Hosting',
  Virtualmin: 'Virtualmin',
  HyperVm: 'HyperVM',
}

/** Human-readable labels for fill types. */
export const FILL_TYPE_LABELS: Record<ServerFillType, string> = {
  LeastFull: 'Add to the least full server',
  FillUntilFull: 'Fill active server until full then switch',
}
