/** What `POST /api/portal/public/domain-check` answers for one domain. */
export interface DomainCheckResult {
  /** The domain that was checked. */
  domain: string
  /** True when nobody has registered it. */
  available: boolean
  /** The registrar's own word for the outcome. */
  status: string
}
