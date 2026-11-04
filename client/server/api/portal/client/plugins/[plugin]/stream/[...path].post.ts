/**
 * POST /api/portal/client/plugins/:plugin/stream/**
 * Calls one of a plugin's streaming routes (`POST /api/client/plugins/{plugin}/{path}`) and passes
 * its server-sent events to the browser as they arrive.
 *
 * Unbuffered end to end: the backend stream is opened with native `fetch`, its body is handed to
 * the browser as a stream, and `X-Accel-Buffering: no` keeps a proxy in front of this server from
 * collecting it. When the browser goes away the backend call is aborted, so the plugin stops
 * writing too, and a browser that left before the backend answered ends the call with no refusal. An answer that is
 * not an event stream is refused with a coded 502 and no sentence. Only the event bytes travel: no upstream header,
 * and never the session's token.
 *
 * @module server/api/portal/client/plugins/[plugin]/stream/[...path].post
 */

import { RouteParamKind } from '~/types/routeparamkind'

/** The code of a backend answer that claimed success but is not an event stream; the BFF writes no sentence. */
export const PLUGIN_STREAM_INVALID = 'PLUGIN_STREAM_INVALID'

/** The media type an event stream is answered with; parameters such as `charset` may follow it. */
const EVENT_STREAM_TYPE = 'text/event-stream'

/**
 * Whether the backend answered with an event stream.
 *
 * @param response - The backend's open response.
 * @returns True when its content type is `text/event-stream`, in any case, with or without parameters.
 */
const isEventStream = (response: Response): boolean =>
  (response.headers.get('content-type') ?? '').split(';')[0]?.trim().toLowerCase() === EVENT_STREAM_TYPE

export default defineEventHandler(async (event) => {
  const plugin = requireRouteParam(event, 'plugin', RouteParamKind.PluginId)
  const path = requirePluginPath(event)
  const body = await readBodyObject(event)

  const upstream = new AbortController()
  event.node.res.once('close', () => upstream.abort())

  let response: Response
  try {
    response = await internalApiStream(event, PluginBackendPaths.route(plugin, `${path}${pluginQuery(event)}`), {
      body,
      signal: upstream.signal,
    })
  } catch (err: unknown) {
    // The browser left before the backend answered: nobody is waiting for a refusal, so end without one.
    if (upstream.signal.aborted) {
      return null
    }
    throw err
  }

  // A streaming route may still answer with JSON (#641); a 2xx that is not an event stream is never piped to a reader
  // that would wait for events, and its body is not read.
  if (!isEventStream(response)) {
    await response.body?.cancel().catch(() => undefined)
    throw createError({ statusCode: 502, data: { code: PLUGIN_STREAM_INVALID } })
  }

  setResponseHeaders(event, {
    'Content-Type': 'text/event-stream; charset=utf-8',
    'Cache-Control': 'no-cache, no-transform',
    'X-Accel-Buffering': 'no',
    'X-Content-Type-Options': 'nosniff',
  })
  return sendStream(event, response.body ?? new ReadableStream())
})
