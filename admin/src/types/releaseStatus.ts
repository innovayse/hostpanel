/** DTO returned by GET /api/admin/release-status. */
export interface ReleaseStatusDto {
  /** The assembly informational version of the running build, e.g. "1.4.0". */
  installedVersion: string

  /** The latest published GitHub release version, e.g. "1.5.0"; null when not yet fetched. */
  latestVersion: string | null

  /** ISO 8601 timestamp of when the latest release was published; null when latestVersion is. */
  latestPublishedAt: string | null

  /** URL of the GitHub release notes page; null when latestVersion is. */
  releaseNotesUrl: string | null

  /** Shell command to upgrade to the latest version; null when no upgrade is available. */
  upgradeCommand: string | null

  /** True when latestVersion is strictly newer than installedVersion. */
  isUpgradeAvailable: boolean
}
