/**
 * GET /uploads/branding/**
 *
 * Serves operator-uploaded branding files — the logo, the favicon and the icon set
 * generated from them — from the storefront's own origin.
 *
 * **There is already a generic `/uploads/**` proxy** beside this one, added for slide images.
 * This route is deliberately narrower and therefore wins for branding paths. The generic one
 * calls `proxyRequest`, which hands the upstream response back verbatim -- its status, and its
 * `Content-Type`. That is acceptable for slides and not for these: branding files are what a
 * page links as its icon, so they are the ones worth pinning to a known type, refusing by
 * extension, and serving with `nosniff` and a long immutable cache. Slides keep the old
 * behaviour; nothing about them changed.
 *
 * The files are written by the C# API into its web root, which is a different origin to
 * this one. The setting rows store root-relative URLs (`/uploads/branding/...`) so that a
 * domain change never strands them, which means the storefront has to answer for that
 * path itself. This route is that answer: it streams the bytes through rather than
 * redirecting, so the browser never learns the API's address and no CORS negotiation is
 * involved for what is, to the page, a same-origin image.
 *
 * Two things it deliberately does:
 *
 * - **Serves only images, and says so.** The upstream content type is ignored in favour of
 *   one derived from the extension, and `X-Content-Type-Options: nosniff` accompanies it.
 *   The API only ever writes PNGs here, so anything else is a sign something is wrong and
 *   is refused rather than passed to a browser to interpret.
 * - **Refuses to walk.** The captured path is rejected outright if it contains a `..`
 *   segment, a backslash or a leading slash, before it is ever appended to the upstream
 *   URL.
 */

/** Extensions this route will serve, and the type each is served as. */
const ALLOWED_TYPES: Record<string, string> = {
  '.png': 'image/png',
  '.ico': 'image/x-icon',
}

export default defineEventHandler(async (event) => {
  const path = getRouterParam(event, 'path') ?? ''

  // Path traversal guard. The upstream is a file server, so a `..` that survived this far
  // would be resolved by it, not by us.
  const segments = path.split('/')
  if (
    path === ''
    || path.includes('\\')
    || segments.some(s => s === '' || s === '.' || s === '..')
  ) {
    throw createError({ statusCode: 400, statusMessage: 'Bad branding path' })
  }

  const extension = path.slice(path.lastIndexOf('.')).toLowerCase()
  const contentType = ALLOWED_TYPES[extension]
  if (!contentType) {
    throw createError({ statusCode: 404, statusMessage: 'Not a branding image' })
  }

  const config = useRuntimeConfig()

  let body: Buffer
  try {
    const raw = await $fetch<ArrayBuffer>(`/uploads/branding/${path}`, {
      baseURL: config.apiUrl,
      responseType: 'arrayBuffer',
    })

    // Wrapped, not returned bare. h3 recognises a Buffer (it has `.buffer`) and sends the
    // bytes; a plain ArrayBuffer matches none of its response branches and falls through to
    // JSON.stringify, which yields the string "{}" served under Content-Type: image/png.
    // With the immutable cache below, that broken body would then stick in every browser
    // that fetched it once.
    body = Buffer.from(raw)
  } catch {
    // A missing file is a 404 here too. Nothing upstream is worth quoting: the caller is a
    // browser fetching an icon, and the only useful answer is "there is no such icon".
    throw createError({ statusCode: 404, statusMessage: 'Branding image not found' })
  }

  setResponseHeaders(event, {
    'Content-Type': contentType,
    'X-Content-Type-Options': 'nosniff',
    // Immutable is safe and worth a year: each upload is written into its own directory
    // named by a fresh identifier, so a replaced logo arrives at a URL no browser has
    // seen rather than under the old one.
    'Cache-Control': 'public, max-age=31536000, immutable',
  })

  return body
})
