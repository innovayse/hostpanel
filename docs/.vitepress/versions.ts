/**
 * Versioned documentation: which kind of build this is (the latest release, an older release or the
 * development docs) and where each page of it is canonical. Pure helpers, no I/O except
 * `readLatestPages`, so the rules are unit tested without a build.
 *
 * Layout of a published site: the latest release at `<base>`, every older minor release at
 * `<base>vX.Y/` and the development docs at `<base>next/`. The build is told which one it is by
 * `DOCS_VERSION`; see `readVersionEnv`.
 */
import { existsSync, readFileSync } from 'node:fs'

/** The three kinds of documentation build. */
export type DocsVersionKind = 'latest' | 'release' | 'next'

/** Label of the development docs build. */
export const NEXT_VERSION = 'next'

/** What a build knows about the version it is. */
export interface DocsVersion {
  /** Latest release, an older release or the development docs. */
  kind: DocsVersionKind
  /** Label such as `v0.3` or `next`; empty for an unversioned build. */
  label: string
  /** Absolute address of the latest docs without a trailing slash; empty when this build is the latest. */
  latestUrl: string
  /** Address the version list is fetched from at run time; empty when the build is unversioned. */
  versionsUrl: string
  /** Page paths that exist in the latest docs, or null when the list is not known. */
  latestPages: ReadonlySet<string> | null
}

/** The raw settings a build reads from the environment. */
export interface DocsVersionEnv {
  /** `DOCS_VERSION`: `next`, `vX.Y`, or empty for an unversioned build. */
  version?: string
  /** `DOCS_LATEST_VERSION`: label of the latest release (`vX.Y`), empty when there is none. */
  latestVersion?: string
  /** `DOCS_LATEST_URL`: absolute address of the latest docs. */
  latestUrl?: string
  /** `DOCS_VERSIONS_URL`: address of `versions.json` as the browser requests it. */
  versionsUrl?: string
  /** `DOCS_LATEST_PAGES`: path of the JSON file with the page paths of the latest docs. */
  latestPagesFile?: string
}

/** Accepts `vMAJOR.MINOR` and nothing else. */
const RELEASE_LABEL = /^v\d+\.\d+$/

/**
 * Decides the kind of a build. A build is the latest when it is unversioned or its label is the
 * latest label; `next` is the development docs; any other `vX.Y` is an older release.
 * @param version The `DOCS_VERSION` value.
 * @param latestVersion The `DOCS_LATEST_VERSION` value.
 * @returns The kind.
 * @throws When the label is neither empty, `next` nor `vX.Y`.
 */
export const kindOf = (version: string, latestVersion: string): DocsVersionKind => {
  if (version === '') {
    return 'latest'
  }
  if (version === NEXT_VERSION) {
    return 'next'
  }
  if (!RELEASE_LABEL.test(version)) {
    throw new Error(`DOCS_VERSION must be empty, "${NEXT_VERSION}" or "vMAJOR.MINOR", got "${version}"`)
  }
  return version === latestVersion ? 'latest' : 'release'
}

/**
 * Reads the list of page paths of the latest docs, written at image build time.
 * @param file Path of the JSON array, or an empty string.
 * @returns The set of paths, or null when there is no file.
 */
export const readLatestPages = (file: string): ReadonlySet<string> | null => {
  if (!file || !existsSync(file)) {
    return null
  }
  const parsed: unknown = JSON.parse(readFileSync(file, 'utf8'))
  return Array.isArray(parsed) ? new Set(parsed.filter((item): item is string => typeof item === 'string')) : null
}

/**
 * Turns the environment settings into the version of this build.
 * @param env The raw settings.
 * @param pages The page paths of the latest docs, when known.
 * @returns The version; an older release or the development docs without a latest address is an error,
 * because their canonical links would have nowhere to point.
 */
export const resolveVersion = (env: DocsVersionEnv, pages: ReadonlySet<string> | null): DocsVersion => {
  const label = (env.version ?? '').trim()
  const kind = kindOf(label, (env.latestVersion ?? '').trim())
  const latestUrl = (env.latestUrl ?? '').trim().replace(/\/+$/, '')
  if (kind !== 'latest' && !latestUrl) {
    throw new Error('DOCS_LATEST_URL is required when DOCS_VERSION is an older release or "next"')
  }
  return {
    kind,
    label,
    latestUrl: kind === 'latest' ? '' : latestUrl,
    versionsUrl: (env.versionsUrl ?? '').trim(),
    latestPages: kind === 'latest' ? null : pages,
  }
}

/**
 * Whether a page of this build is also a page of the latest docs.
 * @param version The version of the build.
 * @param urlPath The page path without base, for example `staff/orders` or `staff/`.
 * @returns True for the latest build itself, or when the latest list holds the path.
 */
export const existsInLatest = (version: DocsVersion, urlPath: string): boolean => {
  return version.kind === 'latest' || (version.latestPages?.has(urlPath) ?? false)
}

/**
 * The address search engines should treat as the original of a page.
 * Latest and next point at themselves; an older release points at the same page in the latest docs
 * when it exists there and at the latest home when it does not.
 * @param version The version of the build.
 * @param siteUrl Address of this build without a trailing slash.
 * @param urlPath The page path without base.
 * @returns The canonical address.
 */
export const canonicalOf = (version: DocsVersion, siteUrl: string, urlPath: string): string => {
  if (version.kind !== 'release') {
    return `${siteUrl}/${urlPath}`
  }
  return `${version.latestUrl}/${existsInLatest(version, urlPath) ? urlPath : ''}`
}

/**
 * The address the notice banner offers as "go to the latest": the same page when it exists there.
 * @param version The version of the build.
 * @param urlPath The page path without base.
 * @returns The address, or an empty string for the latest build.
 */
export const latestLinkOf = (version: DocsVersion, urlPath: string): string => {
  if (version.kind === 'latest') {
    return ''
  }
  return `${version.latestUrl}/${existsInLatest(version, urlPath) ? urlPath : ''}`
}

/**
 * Whether a page must be kept out of the search index because of its version: all of `next`, and the
 * pages of an older release that the latest docs no longer have.
 * @param version The version of the build.
 * @param urlPath The page path without base.
 * @returns True when the page gets `noindex, follow`.
 */
export const isNoindexByVersion = (version: DocsVersion, urlPath: string): boolean => {
  return version.kind === 'next' || (version.kind === 'release' && !existsInLatest(version, urlPath))
}

/**
 * Whether this build writes a sitemap. Only the latest does; older releases and next are reached
 * through links and the canonical of the latest.
 * @param version The version of the build.
 * @returns True for the latest build.
 */
export const hasSitemap = (version: DocsVersion): boolean => {
  return version.kind === 'latest'
}
