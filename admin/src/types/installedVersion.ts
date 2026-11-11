/** DTO returned by GET /api/admin/version, readable by every staff role. */
export interface InstalledVersionDto {
  /** The assembly informational version of the running build, e.g. "1.4.0". */
  installedVersion: string
}
