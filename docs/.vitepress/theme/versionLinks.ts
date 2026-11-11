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
 *    asked of the server when the list is opened. A definite answer (the page is there, or the server
 *    finally says it is not) is kept for the visit. An answer that is not definite (the request failed,
 *    timed out or the server failed) sends the link to the home page of that version for now and is not
 *    kept, so the next time the list is opened the page is asked for again. The asking is bounded: a capped
 *    number of entries, a few requests at a time, and all of it stops when the caller aborts.
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

/** The statuses that need a special reading; see {@link lookupPage}. */
export enum ProbeStatus {
  /** The server does not allow `HEAD` on the page: ask again with `GET`. */
  MethodNotAllowed = 405,
  /** The server does not implement `HEAD`: ask again with `GET`. */
  NotImplemented = 501,
  /** The server gave up waiting for the request: a passing failure, not an answer about the page. */
  RequestTimeout = 408,
  /** The server is rate limiting: a passing failure, not an answer about the page. */
  TooManyRequests = 429,
}

/** What a lookup of a page found out. */
export enum PageLookup {
  /** The server answered with a 2xx status: the page is there. */
  Exists = 'exists',
  /** The server finally answered with a status that is not 2xx and not passing (404, 403, 400, a redirect and so on): the page is not there. */
  Missing = 'missing',
  /** No answer about the page: the request failed or timed out, or the server failed (5xx, 408, 429). */
  Unknown = 'unknown',
}

/** The lowest status of a server failure. */
const SERVER_ERROR_FROM = 500

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
  /** The status code; a redirect that was not followed is 3xx or, in a browser, 0 (an opaque redirect). */
  status: number
  /** The body, when there is one; the body of a `GET` answer is cancelled as soon as the status is known. */
  body?: { cancel: () => Promise<void> } | null
}

/** A request function shaped like `fetch`, narrowed to what the module uses. */
export type Probe = (url: string, init: { method: ProbeMethod; signal: AbortSignal; redirect: 'manual' }) => Promise<ProbeResponse>

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

/** The most entries of `versions.json` that are read; the rest of a longer file is ignored. */
export const MAX_LISTED_ENTRIES = 100

/**
 * The most times a link is percent decoded; a link that still changes after this many is refused. The bound is
 * deliberate and fails closed: a link that needs more passes is nested deeper than any real path, so it is
 * treated as hostile and dropped rather than decoded further or let through half checked.
 */
export const MAX_DECODE_PASSES = 5

/**
 * Characters a link may not hold in any decoded form: whitespace, control characters (a null included),
 * a backslash, and the full width slash (U+FF0F) and backslash (U+FF3C), which some servers fold to the
 * ASCII ones. Browsers and servers read all of them differently from the check.
 */
// eslint-disable-next-line no-control-regex -- the control characters are exactly what this pattern refuses
const FORBIDDEN_CHARACTERS = /[\s\\\u0000-\u001f\u007f／＼]/

/**
 * Percent decodes a link again and again until it stops changing, so `%252f` (a slash escaped twice) is
 * seen as the slash it becomes after the server's own decoding.
 *
 * @param link - The link from the file.
 * @returns Every form of the link, the link itself first and the fully decoded one last; `null` when a form
 * cannot be decoded (a stray `%`, an invalid or overlong UTF-8 sequence such as `%c0%af`) or the link still
 * changes after {@link MAX_DECODE_PASSES} passes.
 */
const decodedForms = (link: string): string[] | null => {
  const forms: string[] = [link]
  for (let pass = 0; pass < MAX_DECODE_PASSES; pass += 1) {
    const current = forms[forms.length - 1]
    let next: string
    try {
      next = decodeURIComponent(current)
    } catch {
      return null
    }
    if (next === current) {
      return forms
    }
    forms.push(next)
  }
  return null
}

/**
 * Whether the forms of a link hide a separator, a forbidden character or a dot segment. A slash that is
 * new in a form was an escaped slash in the one before, so it is refused; the raw link holds slashes of
 * its own, which are counted, not refused.
 *
 * @param forms - The forms from {@link decodedForms}, the link first.
 * @returns `true` when the link must be refused.
 */
const hidesPathTrick = (forms: string[]): boolean => {
  /** Counts the slashes of a form. */
  const slashes = (text: string): number => text.split('/').length - 1
  const newSlash = forms.some((form, index) => index > 0 && slashes(form) > slashes(forms[index - 1]))
  const dotSegment = forms[forms.length - 1].split('/').some((segment) => segment === '.' || segment === '..')
  return newSlash || dotSegment || forms.some((form) => FORBIDDEN_CHARACTERS.test(form))
}

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
  // Whitespace, control characters (a null included), backslashes and the full width slash are read differently by browsers and servers.
  if (FORBIDDEN_CHARACTERS.test(link)) {
    return null
  }
  // `URL` leaves percent escapes as they are, so `/docs/..%2f..%2fadmin/` would pass the root check below and
  // a server that decodes it, once or several times, would climb out of the docs. The link is decoded until
  // it is stable and every form is checked; one that cannot be decoded (a stray `%`, overlong `%c0%af`) is refused.
  const forms = decodedForms(link)
  if (forms === null || hidesPathTrick(forms)) {
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
 * @returns The usable entries in file order, the first one of a label winning, taken from the first {@link MAX_LISTED_ENTRIES}
 * items of the file; empty when the file has no usable list.
 */
export const parseVersions = (body: unknown, root: string): VersionEntry[] => {
  const list = typeof body === 'object' && body !== null ? (body as { versions?: unknown }).versions : undefined
  if (!Array.isArray(list)) {
    return []
  }
  const entries: VersionEntry[] = []
  const seen = new Set<string>()
  // A file is read only up to the cap, so a huge or hostile list costs a bounded amount of work and memory.
  for (const item of list.slice(0, MAX_LISTED_ENTRIES)) {
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
 * Reads the status of an answer about a page.
 *
 * A 2xx status means the page is there. A 5xx status, a 408 and a 429 are passing failures of the server,
 * not an answer about the page, so they are unknown. Every other status is final and means the page is not
 * there: 404, 403 and 400, and also a redirect, because the request is made with `redirect: 'manual'` and a
 * page that sends the reader elsewhere is not the same page in that version (in a browser the answer is an
 * opaque redirect with status 0).
 *
 * @param response - The answer.
 * @returns The outcome the status stands for.
 */
const lookupOf = (response: ProbeResponse): PageLookup => {
  if (response.ok) {
    return PageLookup.Exists
  }
  if (response.status >= SERVER_ERROR_FROM || response.status === ProbeStatus.RequestTimeout || response.status === ProbeStatus.TooManyRequests) {
    return PageLookup.Unknown
  }
  return PageLookup.Missing
}

/**
 * Asks whether a page exists, without opening it.
 *
 * A server that does not allow `HEAD` (405 or 501) is asked again with `GET`, and the body of that answer
 * is cancelled at once. Redirects are not followed (see {@link lookupOf}). The outcome is
 * {@link PageLookup.Unknown} when there is no usable answer: the request failed, it timed out, the caller
 * aborted, or the server failed; the caller must not remember such an outcome.
 *
 * @param url - Path of the page.
 * @param probe - A `fetch`-like request function.
 * @param timeoutMs - How long to wait for an answer.
 * @param signal - Stops the lookup when it aborts; the outcome is then {@link PageLookup.Unknown}.
 * @returns What was found out about the page.
 */
export const lookupPage = async (url: string, probe: Probe, timeoutMs: number = PROBE_TIMEOUT_MS, signal?: AbortSignal): Promise<PageLookup> => {
  if (signal?.aborted) {
    return PageLookup.Unknown
  }
  const controller = new AbortController()
  /** Cancels the request in flight; shared by the timeout and the caller's signal. */
  const stop = (): void => controller.abort()
  signal?.addEventListener('abort', stop, { once: true })
  const timer = setTimeout(stop, timeoutMs)
  try {
    let response = await probe(url, { method: ProbeMethod.Head, signal: controller.signal, redirect: 'manual' })
    if (response.status === ProbeStatus.MethodNotAllowed || response.status === ProbeStatus.NotImplemented) {
      response = await probe(url, { method: ProbeMethod.Get, signal: controller.signal, redirect: 'manual' })
      // Only the status is wanted; do not let the page download.
      await response.body?.cancel().catch(() => undefined)
    }
    return lookupOf(response)
  } catch {
    return PageLookup.Unknown
  } finally {
    clearTimeout(timer)
    signal?.removeEventListener('abort', stop)
  }
}

/** What {@link findPages} needs besides the entries. */
export interface FindPagesOptions {
  /** Stops the work: no new request starts after it aborts and the running ones are cancelled. */
  signal: AbortSignal
  /** Definite answers already known (true: there, false: not there), by page address; unknown outcomes are never stored. Filled by the call and kept by the caller between calls. */
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
 * asked for again. Only a definite answer is cached; a page whose outcome is unknown (see
 * {@link PageLookup.Unknown}) links to the home page of its version for now and is asked for again the next
 * time. When the signal aborts, nothing is returned and nothing more is cached.
 *
 * @param entries - The versions to look in, never the open one.
 * @param pagePath - Path of the open page below the site base.
 * @param probe - A `fetch`-like request function.
 * @param options - The abort signal and the cache.
 * @returns The link by label, for the entries that were answered; an empty map when the signal aborted.
 */
export const findPages = async (entries: VersionEntry[], pagePath: string, probe: Probe, options: FindPagesOptions): Promise<Map<string, string>> => {
  const { signal, cache } = options
  const queue = entries.slice(0, MAX_PROBED_ENTRIES)
  // A Map, not an object: a label such as `constructor`, `toString` or `__proto__` must not read a member inherited from `Object.prototype`.
  const links = new Map<string, string>()
  let next = 0

  /** Takes entries off the queue one after another until it is empty or the work is aborted. */
  const worker = async (): Promise<void> => {
    while (next < queue.length && !signal.aborted) {
      const entry = queue[next]
      next += 1
      const url = pageIn(entry, pagePath)
      let exists = cache.get(url)
      if (exists === undefined) {
        const found = await lookupPage(url, probe, PROBE_TIMEOUT_MS, signal)
        if (signal.aborted) {
          return
        }
        exists = found === PageLookup.Exists
        // An unknown outcome is not an answer about the page: it is not kept, so the next lookup asks again.
        if (found !== PageLookup.Unknown) {
          if (cache.size >= MAX_CACHED_ANSWERS) {
            cache.clear()
          }
          cache.set(url, exists)
        }
      }
      links.set(entry.label, hrefFor(entry, pagePath, exists))
    }
  }

  await Promise.all(Array.from({ length: Math.min(MAX_CONCURRENT_PROBES, queue.length) }, worker))
  return signal.aborted ? new Map<string, string>() : links
}
