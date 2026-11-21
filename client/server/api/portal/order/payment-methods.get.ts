/**
 * GET /api/portal/order/payment-methods
 * Returns the payment gateways from the C# backend that may pay for what is asked about.
 *
 * `productIds` (comma separated ids of the cart's products) and `invoiceId` (the payer's own invoice) name what is
 * being paid for: the backend leaves out the methods the product groups of those do not allow, or answers
 * `PAYMENT_NO_COMMON_METHOD` when no method is allowed by all of them. With neither, every gateway on offer is listed.
 * No authentication is needed for a cart; an invoice is only answered for its signed-in owner.
 *
 * @module server/api/portal/order/payment-methods.get
 */

import { BAD_QUERY, readQueryString } from '~/server/utils/readQueryString'

/** An id list or id: digits only, so nothing but numbers reaches the backend's query string. */
const IDS_PATTERN = /^\d+(,\d+)*$/

/** Most products a cart may name; the backend refuses more. */
const MAX_PRODUCT_IDS = 100

/**
 * Reads the products of the cart from the query.
 *
 * @param value - The raw `productIds` value: comma separated ids, or absent.
 * @returns The ids, empty when none were named.
 * @throws H3Error 400 carrying `data.code = BAD_QUERY` when the value is not a list of ids.
 */
const productIdsOf = (value: string | undefined): string[] => {
  if (value === undefined || value === '') {
    return []
  }
  const ids = value.split(',')
  if (!IDS_PATTERN.test(value) || ids.length > MAX_PRODUCT_IDS) {
    throw createError({ statusCode: 400, data: { code: BAD_QUERY } })
  }
  return ids
}

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const productIds = productIdsOf(readQueryString(query.productIds))
  const invoiceId = readQueryString(query.invoiceId)
  if (invoiceId !== undefined && !/^\d+$/.test(invoiceId)) {
    throw createError({ statusCode: 400, data: { code: BAD_QUERY } })
  }

  const params = new URLSearchParams()
  for (const id of productIds) {
    params.append('productIds', id)
  }
  if (invoiceId !== undefined) {
    params.set('invoiceId', invoiceId)
  }
  const search = params.toString()
  return await internalApiCall<unknown[]>(event, search ? `/payment-methods?${search}` : '/payment-methods')
})
