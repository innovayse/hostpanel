/**
 * Rules behind the links of the version switcher.
 *
 * `versions.json` is published by the deployment, next to the latest docs, and is read at run time, so
 * the switcher cannot trust it: a wrong or tampered entry must not send a reader to another site or out
 * of the documentation. Two rules follow.
 *
 * 1. A link is used only when it is a path of this site below the root of the docs: not an absolute
 *    address, not `//host`, not a path that climbs out with `..`.
 * 2. "The same page in another version" is offered only when that page exists there. Whether it exists is
 *    asked of the server when the list is opened; when it does not, or the answer does not come, the link
 *    goes to the home page of that version.
 *
 * The module has no Vue and no VitePress import, so a unit test can run it in plain Node. The request is
 * handed in by the caller.
 */

/** The kinds a `versions.json` entry can have. */
export type VersionKind = 'latest' | 'release' | 'next'

/** One entry of `versions.json` that passed the checks. */
export interface VersionEntry {
  /** Name shown in the list, for example `v0.3` or `next`. */
  label: string
  /** Path of the version's home page below the root of the docs, with a trailing slash. */
  link: string
  /** Whether the entry is the latest release, an older release or the development docs. */
  kind: VersionKind
}

/** The part of a response this module reads. */
export interface ProbeResponse {
  /** Whether the status is 2xx. */
  ok: boolean
  /** The status code. */
  status: number
}

/** A request function shaped like `fetch`, narrowed to what the module uses. */
export type Probe = (url: string, init: { method: 'HEAD' | 'GET'; signal: AbortSignal }) => Promise<ProbeResponse>

/** How long a page is waited for before the home page of the version is used instead, in milliseconds. */
export const PROBE_TIMEOUT_MS = 4000

/** Origin used to read a path as a URL; it never leaves this module and is not a real address. */
const PROBE_ORIGIN = 'http://docs.invalid'

/** The kinds that `versions.json` may name. */
const KINDS: ReadonlySet<string> = new Set(['latest', 'release', 'next'])

/**
 * Root of the whole docs site (where the latest docs live) from the base the open build is served under.
 * The latest is served at the root, an older release at `<root>vX.Y/` and the development docs at
 * `<root>next/`, so for those two the last segment is cut off.
 *
 * @param base - The site base of the open build, for example `/docs/v0.3/`.
 * @param kind - The kind of the open build.
 * @param label - The label of the open build, for example `v0.3`.
 * @returns The root, with a leading and a trailing slash.
 */
export function docsRootOf(base: string, kind: string, label: string): string {
  const normalized = `/${base.replace(/^\/+|\/+$/g, '')}/`.replace(/^\/\/$/, '/')
  const suffix = `${label}/`
  if (kind !== 'latest' && label !== '' && normalized.endsWith(`/${suffix}`)) {
    return normalized.slice(0, normalized.length - suffix.length)
  }
  return normalized
}

/**
 * Checks one link of `versions.json` and returns it as a path of this site.
 *
 * @param link - The value from the file; anything.
 * @param root - Root of the docs, from {@link docsRootOf}.
 * @returns The path with a trailing slash, or `null` when it is not a path below the root.
 */
export function safeVersionLink(link: unknown, root: string): string | null {
  if (typeof link !== 'string' || link === '') {
    return null
  }
  // Whitespace, control characters and backslashes are read differently by browsers and servers.
  if (/[\s\\\u0000-\u001f\u007f]/.test(link)) {
    return null
  }
  // A path starts with one slash. A scheme or `//host` makes the browser leave the site.
  if (!link.startsWith('/') || link.startsWith('//')) {
    return null
  }
  let parsed: URL
  try {
    parsed = new URL(link, PROBE_ORIGIN)
  } catch {
    return null
  }
  if (parsed.origin !== PROBE_ORIGIN) {
    return null
  }
  // `pathname` has `..` and `%2e%2e` already resolved, so a path that climbs out is caught here.
  const path = parsed.pathname.endsWith('/') ? parsed.pathname : `${parsed.pathname}/`
  return path.startsWith(root) ? path : null
}

/**
 * Keeps the entries of `versions.json` that are well formed and point below the root of the docs.
 *
 * @param body - The parsed file; anything.
 * @param root - Root of the docs, from {@link docsRootOf}.
 * @returns The usable entries in file order; empty when the file has no usable list.
 */
export function parseVersions(body: unknown, root: string): VersionEntry[] {
  const list = typeof body === 'object' && body !== null ? (body as { versions?: unknown }).versions : undefined
  if (!Array.isArray(list)) {
    return []
  }
  const entries: VersionEntry[] = []
  for (const item of list) {
    if (typeof item !== 'object' || item === null) {
      continue
    }
    const { label, link, kind } = item as Record<string, unknown>
    const safe = safeVersionLink(link, root)
    if (typeof label !== 'string' || label.trim() === '' || typeof kind !== 'string' || !KINDS.has(kind) || safe === null) {
      continue
    }
    entries.push({ label, link: safe, kind: kind as VersionKind })
  }
  return entries
}

/**
 * Address of a page in a version.
 *
 * @param entry - The version.
 * @param pagePath - Path of the page below the site base, no leading slash, for example `operators/install`.
 * @returns The path of the same page in that version.
 */
export function pageIn(entry: VersionEntry, pagePath: string): string {
  return `${entry.link}${pagePath}`
}

/**
 * Asks whether a page exists, without opening it.
 *
 * A server that does not allow `HEAD` (405 or 501) is asked again with `GET`. Any other failure, a network
 * error and a timeout included, counts as "does not exist", so the caller falls back to the home page.
 *
 * @param url - Path of the page.
 * @param probe - A `fetch`-like request function.
 * @param timeoutMs - How long to wait for an answer.
 * @returns `true` only when the server answered with a 2xx status.
 */
export async function pageExists(url: string, probe: Probe, timeoutMs: number = PROBE_TIMEOUT_MS): Promise<boolean> {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  try {
    let response = await probe(url, { method: 'HEAD', signal: controller.signal })
    if (response.status === 405 || response.status === 501) {
      response = await probe(url, { method: 'GET', signal: controller.signal })
    }
    return response.ok
  } catch {
    return false
  } finally {
    clearTimeout(timer)
  }
}

/**
 * The link to offer for a version: the same page when it exists there, the home page of the version when
 * it does not.
 *
 * @param entry - The version.
 * @param pagePath - Path of the open page below the site base.
 * @param exists - Whether the same page exists in that version.
 * @returns The path to link to.
 */
export function hrefFor(entry: VersionEntry, pagePath: string, exists: boolean): string {
  return exists ? pageIn(entry, pagePath) : entry.link
}
