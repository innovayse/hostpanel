/**
 * The query a slot read forwards: the slot's context, today only `serviceId`. Read in one place for
 * the index and the widget routes, so both accept exactly the same shapes, and through the same
 * rules every other route uses: `readQueryString` for "one string or nothing" and the positive
 * 32-bit integer rule of `requireRouteParam`'s `Int` kind (`isPositiveIntKey`), so a service id is
 * never accepted here that a route parameter would refuse.
 *
 * @module server/utils/slotQuery
 */

import type { H3Event } from 'h3'
import { BAD_QUERY, readQueryString } from '~/server/utils/readQueryString'
import { isPositiveIntKey } from '~/server/utils/requireRouteParam'

/**
 * Reads and checks a slot read's context from the query.
 *
 * @param event - The request.
 * @returns The query string to forward, with its `?`, or empty when no context was sent.
 * @throws A coded 400 (`BAD_QUERY`) when `serviceId` is repeated or not a positive 32-bit integer.
 */
export const readSlotQuery = (event: H3Event): string => {
  const serviceId = readQueryString(getQuery(event).serviceId)
  if (serviceId === undefined) {
    return ''
  }
  if (!isPositiveIntKey(serviceId)) {
    throw createError({ statusCode: 400, data: { code: BAD_QUERY } })
  }
  return `?serviceId=${serviceId}`
}
