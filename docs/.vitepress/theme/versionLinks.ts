/**
 * Rules behind the links of the version switcher.
 *
 * `versions.json` is published by the deployment, next to the latest docs, and is read at run time, so
 * the switcher cannot trust it: a wrong or tampered entry must not send a reader to another site or out
 * of the documentation. Two rules follow.
 *
 * 1. A link is used only when it is a path of this site below the root of the docs: not an absolute
 *    address, not `//host`, not a path that climbs out with `..`, not a path that hides a slash, a
 *    backslash or a null in a percent escape.
 * 2. "The same page in another version" is offered only when that page exists there. Whether it exists is
 *    asked of the server when the list is opened; when it does not, or the answer does not come, the link
 *    goes to the home page of that version. The asking is bounded: a capped number of entries, a few
 *    requests at a time, one answer kept per page, and all of it stops when the caller aborts.
 *
 * The module has no Vue and no VitePress import, so a unit test can run it in plain Node. The request is
 * handed in by the caller.
 */

/** The kinds a `versions.json` entry can have. */
export enum VersionKind {
  /** The latest release. */
  Latest = 'latest',
  /** An older release. */
  Release = 'release',
  /** The development docs. */
  Next = 'next',
}

/** The request methods used to look for a page. */
export enum ProbeMethod {
  /** Asks for the status only; tried first. */
  Head = 'HEAD',
  /** Asks for the page; used only when the server does not allow `HEAD`. */
  Get = 'GET',
}

/** The statuses that make the lookup ask again with another method; every other non-2xx status means "missing". */
export enum ProbeStatus {
  /** The server does not allow `HEAD` on the page. */
  MethodNotAllowed = 405,
  /** The server does not implement `HEAD`. */
  NotImplemented = 501,
}

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
  /** The body, when there is one; the body of a `GET` answer is cancelled as soon as the status is known. */
  body?: { cancel: () => Promise<void> } | null
}

/** A request function shaped like `fetch`, narrowed to what the module uses. */
export type Probe = (url: string, init: { method: ProbeMethod; signal: AbortSignal }) => Promise<ProbeResponse>

/** How long a page is waited for before the home page of the version is used instead, in milliseconds. */
export const PROBE_TIMEOUT_MS = 4000

/** The most entries of `versions.json` whose page is looked for; the others link to their home page. */
export const MAX_PROBED_ENTRIES = 20

/** The most page requests in flight at the same time. */
export const MAX_CONCURRENT_PROBES = 4

/** The most answers kept; the cache is emptied before it would grow past this. */
export const MAX_CACHED_ANSWERS = 200

/** Origin used to read a path as a URL; it never leaves this module and is not a real address. */
const PROBE_ORIGIN = 'http://docs.invalid'

/** The kinds that `versions.json` may name. */
const KINDS: ReadonlySet<string> = new Set(Object.values(VersionKind))

/**
 * Percent escapes a server may decode into a path separator or a string end after the check: a slash
 * (`%2f`), a backslash (`%5c`) and a null (`%00`), in any letter case.
 */
const ENCODED_SEPARATOR = /%(2f|5c|00)/i

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
export const docsRootOf = (base: string, kind: string, label: string): string => {
  const normalized = `/${base.replace(/^\/+|\/+$/g, '')}/`.replace(/^\/\/$/, '/')
  const suffix = `${label}/`
  if (kind !== VersionKind.Latest && label !== '' && normalized.endsWith(`/${suffix}`)) {
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
export const safeVersionLink = (link: unknown, root: string): string | null => {
  if (typeof link !== 'string' || link === '') {
    return null
  }
  // Whitespace, control characters (a null included) and backslashes are read differently by browsers and servers.
  if (/[\s\\\u0000-\u001f\u007f]/.test(link)) {
    return null
  }
  // `URL` leaves these escapes as they are, so `/docs/..%2f..%2fadmin/` would pass the root check below and
  // a server that decodes it would climb out of the docs. A real docs path never needs them.
  if (ENCODED_SEPARATOR.test(link)) {
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
 * @returns The usable entries in file order, the first one of a label winning; empty when the file has no usable list.
 */
export const parseVersions = (body: unknown, root: string): VersionEntry[] => {
  const list = typeof body === 'object' && body !== null ? (body as { versions?: unknown }).versions : undefined
  if (!Array.isArray(list)) {
    return []
  }
  const entries: VersionEntry[] = []
  const seen = new Set<string>()
  for (const item of list) {
    if (typeof item !== 'object' || item === null) {
      continue
    }
    const { label, link, kind } = item as Record<string, unknown>
    const safe = safeVersionLink(link, root)
    if (typeof label !== 'string' || label.trim() === '' || typeof kind !== 'string' || !KINDS.has(kind) || safe === null) {
      continue
    }
    // A label is the key of the list and of the answers kept for it, so a second entry with it is dropped.
    if (seen.has(label)) {
      continue
    }
    seen.add(label)
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
export const pageIn = (entry: VersionEntry, pagePath: string): string => {
  return `${entry.link}${pagePath}`
}

/**
 * Asks whether a page exists, without opening it.
 *
 * A server that does not allow `HEAD` (405 or 501) is asked again with `GET`, and the body of that answer
 * is cancelled at once. Any other answer that is not 2xx, a 400 or a 403 to `HEAD` included, and any
 * failure, a network error, an abort and a timeout included, counts as "does not exist", so the caller
 * falls back to the home page.
 *
 * @param url - Path of the page.
 * @param probe - A `fetch`-like request function.
 * @param timeoutMs - How long to wait for an answer.
 * @param signal - Stops the lookup when it aborts; the answer is then `false`.
 * @returns `true` only when the server answered with a 2xx status.
 */
export const pageExists = async (url: string, probe: Probe, timeoutMs: number = PROBE_TIMEOUT_MS, signal?: AbortSignal): Promise<boolean> => {
  if (signal?.aborted) {
    return false
  }
  const controller = new AbortController()
  /** Cancels the request in flight; shared by the timeout and the caller's signal. */
  const stop = (): void => controller.abort()
  signal?.addEventListener('abort', stop, { once: true })
  const timer = setTimeout(stop, timeoutMs)
  try {
    let response = await probe(url, { method: ProbeMethod.Head, signal: controller.signal })
    if (response.status === ProbeStatus.MethodNotAllowed || response.status === ProbeStatus.NotImplemented) {
      response = await probe(url, { method: ProbeMethod.Get, signal: controller.signal })
      // Only the status is wanted; do not let the page download.
      await response.body?.cancel().catch(() => undefined)
    }
    return response.ok
  } catch {
    return false
  } finally {
    clearTimeout(timer)
    signal?.removeEventListener('abort', stop)
  }
}

/** What {@link findPages} needs besides the entries. */
export interface FindPagesOptions {
  /** Stops the work: no new request starts after it aborts and the running ones are cancelled. */
  signal: AbortSignal
  /** Answers already known, by page address; filled by the call and kept by the caller between calls. */
  cache: Map<string, boolean>
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
export const hrefFor = (entry: VersionEntry, pagePath: string, exists: boolean): string => {
  return exists ? pageIn(entry, pagePath) : entry.link
}

/**
 * Looks for one page in several versions and returns the link to use for each.
 *
 * At most {@link MAX_PROBED_ENTRIES} entries are asked, {@link MAX_CONCURRENT_PROBES} at a time; an entry
 * past the cap gets no link here, so the caller shows its home page. An answer already in the cache is not
 * asked for again. When the signal aborts, nothing is returned and nothing more is cached.
 *
 * @param entries - The versions to look in, never the open one.
 * @param pagePath - Path of the open page below the site base.
 * @param probe - A `fetch`-like request function.
 * @param options - The abort signal and the cache.
 * @returns The link by label, for the entries that were answered.
 */
export const findPages = async (entries: VersionEntry[], pagePath: string, probe: Probe, options: FindPagesOptions): Promise<Record<string, string>> => {
  const { signal, cache } = options
  const queue = entries.slice(0, MAX_PROBED_ENTRIES)
  const links: Record<string, string> = {}
  let next = 0

  /** Takes entries off the queue one after another until it is empty or the work is aborted. */
  const worker = async (): Promise<void> => {
    while (next < queue.length && !signal.aborted) {
      const entry = queue[next]
      next += 1
      const url = pageIn(entry, pagePath)
      let exists = cache.get(url)
      if (exists === undefined) {
        exists = await pageExists(url, probe, PROBE_TIMEOUT_MS, signal)
        if (signal.aborted) {
          return
        }
        if (cache.size >= MAX_CACHED_ANSWERS) {
          cache.clear()
        }
        cache.set(url, exists)
      }
      links[entry.label] = hrefFor(entry, pagePath, exists)
    }
  }

  await Promise.all(Array.from({ length: Math.min(MAX_CONCURRENT_PROBES, queue.length) }, worker))
  return signal.aborted ? {} : links
}
