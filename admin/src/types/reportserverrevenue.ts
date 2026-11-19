/** One server of the server revenue forecasts report; every amount is in the report's base currency. */
export interface ReportServerRevenueRow {
  /** Server id. */
  serverId: number
  /** Server display name. */
  serverName: string
  /** Server hostname. */
  hostname: string
  /** Active services provisioned on the server. */
  activeServices: number
  /** What the active services renew for each month, the longer cycles spread over their months. */
  monthlyRevenue: number
  /** Twelve months of the monthly revenue. */
  annualRevenue: number
}

/** The server revenue forecasts report: the servers with the base currency their amounts are in. */
export interface ReportServerRevenue {
  /** ISO 4217 code of the base currency the amounts are converted to. */
  currency: string
  /** One row per server. */
  rows: ReportServerRevenueRow[]
}
