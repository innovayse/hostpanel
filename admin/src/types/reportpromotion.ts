/** One promotion code of the promotions report; the amount is in the report's base currency. */
export interface ReportPromotionRow {
  /** Promotion code as recorded on the services. */
  code: string
  /** Services, in any status, that carry the code. */
  services: number
  /** Active services that carry the code. */
  activeServices: number
  /** What the active services renew for each month, the longer cycles spread over their months. */
  monthlyRevenue: number
}

/** The promotions report: the codes with the base currency their amounts are in. */
export interface ReportPromotions {
  /** ISO 4217 code of the base currency the amounts are converted to. */
  currency: string
  /** One row per promotion code, the most used first. */
  rows: ReportPromotionRow[]
}
