/**
 * Version ordering for the plugin details page: which of two plugin versions is newer, and how a
 * listed version relates to the installed one. The registry sends semantic versions
 * (`1.2.3`, optionally with a `-pre` part), so the comparison is numeric per segment, never textual.
 */

/** How a listed version stands against the installed one. */
export const VersionRelation = {
  /** No version is installed, so any version is a fresh install. */
  Fresh: 'fresh',
  /** The same version as the installed one. */
  Current: 'current',
  /** Newer than the installed version. */
  Newer: 'newer',
  /** Older than the installed version; installing it is a downgrade. */
  Older: 'older',
} as const

/** One of the {@link VersionRelation} values. */
export type VersionRelation = (typeof VersionRelation)[keyof typeof VersionRelation]

/**
 * Splits a version into its numeric core and its pre-release part.
 *
 * @param version - A version such as `1.2.3-beta.1+build5`.
 * @returns The numeric segments and the pre-release text (empty for a release).
 */
const parse = (version: string): { core: number[]; pre: string } => {
  const withoutBuild = version.split('+')[0] ?? ''
  const dash = withoutBuild.indexOf('-')
  const corePart = dash === -1 ? withoutBuild : withoutBuild.slice(0, dash)
  const pre = dash === -1 ? '' : withoutBuild.slice(dash + 1)
  const core = corePart.split('.').map((segment) => {
    const value = Number.parseInt(segment, 10)
    return Number.isNaN(value) ? 0 : value
  })
  return { core, pre }
}

/**
 * Compares two versions.
 *
 * @param a - First version.
 * @param b - Second version.
 * @returns A negative number when `a` is older, zero when equal, a positive number when `a` is newer.
 */
export const compareVersions = (a: string, b: string): number => {
  const left = parse(a)
  const right = parse(b)
  const length = Math.max(left.core.length, right.core.length)
  for (let index = 0; index < length; index++) {
    const difference = (left.core[index] ?? 0) - (right.core[index] ?? 0)
    if (difference !== 0) {
      return difference
    }
  }
  if (left.pre === right.pre) {
    return 0
  }
  // A release is newer than any pre-release of the same numbers.
  if (left.pre === '') {
    return 1
  }
  if (right.pre === '') {
    return -1
  }
  return left.pre.localeCompare(right.pre, 'en', { numeric: true })
}

/**
 * Tells how a listed version relates to the installed one.
 *
 * @param version - The listed version.
 * @param installed - The installed version, null when the plugin is not installed.
 * @returns The relation.
 */
export const relationTo = (version: string, installed: string | null): VersionRelation => {
  if (installed === null) {
    return VersionRelation.Fresh
  }
  const order = compareVersions(version, installed)
  if (order === 0) {
    return VersionRelation.Current
  }
  return order > 0 ? VersionRelation.Newer : VersionRelation.Older
}
