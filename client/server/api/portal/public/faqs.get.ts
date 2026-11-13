/**
 * GET /api/portal/public/faqs
 *
 * The active questions, in display order. The language reaches the backend as `X-Language`
 * (stamped by `internalApiCall`). The topic is passed through as the API serializes it, the
 * enum name, which is the value of the client's `FaqCategory`.
 *
 * @module server/api/portal/public/faqs.get
 */

import type { FAQ } from '~/types/faq'

/** Handler: the backend's active questions, passed through as sent. */
export default defineEventHandler(async (event): Promise<FAQ[]> =>
  await internalApiCall<FAQ[]>(event, '/public/faqs'))
