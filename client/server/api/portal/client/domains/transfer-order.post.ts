/**
 * POST /api/portal/client/domains/transfer-order
 *
 * Places an order to transfer a domain in to the authenticated client's own account.
 *
 * This had no backend behind it and answered 404 for every attempt. It now reaches
 * `POST /me/domains/transfer-order`, whose `TransferMyDomainCommand` carries no client id and
 * resolves the owning account from the credential — not the admin `POST /api/domains/transfer`,
 * which is `[Authorize(Roles = Admin,Reseller)]` and takes its client from the request body.
 *
 * A transfer-in is a purchase, so the API answers with an order and the invoice raised for it;
 * the registrar is not called until that invoice is paid. That is why the reply carries an
 * invoice id for the page to link to rather than a domain id.
 *
 * The field names are translated here rather than in the page. The form has carried
 * `domain`/`eppcode`/`paymentmethod` since the portal talked to a different backend, and the
 * command spells them `domainName`/`eppCode`/`paymentMethod`; adapting the portal's shape to
 * the API's is what this layer is for. Nothing is validated here — the command's validator
 * refuses an empty domain, a short EPP code and a period outside 1–10, and its sentences are
 * the ones the page shows.
 */

/** The order the API raised, and the invoice the client has to pay for it. */
interface TransferOrderResult {
  /** Primary key of the new order. */
  orderId: number
  /** Primary key of the invoice raised for that order. */
  invoiceId: number
}

export default defineEventHandler(async (event): Promise<TransferOrderResult> => {
  const body = await readBody<Record<string, unknown>>(event)

  return await internalApiCall<TransferOrderResult>(event, '/me/domains/transfer-order', {
    method: 'POST',
    body: {
      domainName: body.domain,
      eppCode: body.eppcode,
      years: body.years,
      paymentMethod: body.paymentmethod
    }
  })
})
