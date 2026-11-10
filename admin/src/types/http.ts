/**
 * The closed value sets of the HTTP area, one file for the area: the verbs the admin sends to the
 * API and the media types it names. The members are the strings that go on the wire.
 */

/** HTTP verb of a request. A request that names none is a GET. */
export enum HttpMethod {
  /** Read a resource. */
  Get = 'GET',
  /** Create a resource or run an action. */
  Post = 'POST',
  /** Replace a resource. */
  Put = 'PUT',
  /** Change part of a resource. */
  Patch = 'PATCH',
  /** Remove a resource. */
  Delete = 'DELETE',
}

/** Closed set of media types the admin names: the JSON body type and the image formats uploads allow. */
export enum MimeType {
  /** A JSON document. */
  Json = 'application/json',
  /** A JPEG image. */
  Jpeg = 'image/jpeg',
  /** A PNG image. */
  Png = 'image/png',
  /** A WebP image. */
  Webp = 'image/webp',
  /** A GIF image. */
  Gif = 'image/gif',
  /** A bitmap image. */
  Bmp = 'image/bmp',
  /** An SVG image. */
  Svg = 'image/svg+xml',
  /** A stream of server-sent events. */
  EventStream = 'text/event-stream',
}

/** One server-sent event of a streamed answer, as `useApi().stream` hands it over. */
export interface ServerSentEvent {
  /** The event name; `message` when the stream named none. */
  type: string
  /** The event's data: parsed JSON, or the text when it is not JSON. */
  data: unknown
}
