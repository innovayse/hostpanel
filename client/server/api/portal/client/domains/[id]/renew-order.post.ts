/**
 * POST /api/portal/client/domains/:id/renew-order
 *
 * Places an order to renew one of the signed-in client's own domains.
 *
 * This used to post to `/me/domains/{id}/renew-order`, which does not exist, so every renewal
 * answered 404. The route behind it is `POST /me/domains/{id}/renew`, whose
 * `RenewMyDomainCommand` carries no client id and resolves the owning account from the
 * credential — not the admin `POST /api/domains/{id}/renew`, which is
 * `[Authorize(Roles = Admin,Reseller)]`, calls the registrar immediately and raises no invoice.
 *
 * Repointing it at `/renew` alone would have been worse than the 404: that route answered 204 and
 * the page renders its success panel out of an order id and an invoice id, so it would have
 * announced a renewal nobody was billed for and offered no invoice to pay. A renewal is a
 * purchase, exactly as a transfer-in is, so the API now answers with the order and the invoice
 * raised for it and the registrar is not called until that invoice is paid.
 *
 * The field names are translated here rather than in the page, the same way
 * `transfer-order.post.ts` does it: the form has carried `paymentmethod` since the portal talked
 * to a different backend, and the request spells it `paymentMethod`. Nothing is validated here —
 * `RenewMyDomainValidator` refuses a period outside 1–10 and an empty payment method, and its
 * sentences are the ones the page shows.
 */

/** The order the API raised, and the invoice the client has to pay for it. */
interface RenewOrderResult {
  /** Primary key of the new order. */
  orderId: number
  /** Primary key of the invoice raised for that order. */
  invoiceId: number
}

export default defineEventHandler(async (event): Promise<RenewOrderResult> => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Domain ID is required' })

  const body = await readBody<Record<string, unknown>>(event)

  return await internalApiCall<RenewOrderResult>(event, `/me/domains/${id}/renew`, {
    method: 'POST',
    body: {
      years: body.years,
      paymentMethod: body.paymentmethod
    }
  })
})
