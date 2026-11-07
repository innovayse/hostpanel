/**
 * Fixtures for the Credit tab of a client: a credit with two history lines, one for each way a movement is told (a staff
 * adjustment with its reason, and credit applied to an invoice), and the refusal body of an adjustment.
 */
import type { ClientCredit } from '../../src/types/clientcredit'
import { CreditMovement } from '../../src/types/creditmovement'

/** `GET /api/clients/7/credit`: a balance of 25 after a goodwill adjustment and credit applied to invoice 5. */
export const credit: ClientCredit = {
  clientId: 7,
  balance: 25,
  currency: 'USD',
  entries: {
    items: [
      { id: 12, date: '2026-10-02T09:00:00Z', movement: CreditMovement.InvoiceApplied, change: -15, balanceAfter: 25, invoiceId: 5, description: 'Credit applied to Invoice #5' },
      { id: 11, date: '2026-10-01T09:00:00Z', movement: CreditMovement.AdminAdd, change: 40, balanceAfter: 40, invoiceId: null, description: 'Goodwill after the outage' },
    ],
    pageNumber: 1,
    pageSize: 25,
    totalCount: 2,
    totalPages: 1,
  },
}

/**
 * The credit after an adjustment landed: the new balance and the new line on top.
 *
 * @param change - The signed change the adjustment made.
 * @param reason - The reason staff gave.
 * @returns The credit the endpoint answers with afterwards.
 */
export const creditAfter = (change: number, reason: string): ClientCredit => ({
  ...credit,
  balance: credit.balance + change,
  entries: {
    ...credit.entries,
    items: [
      { id: 13, date: '2026-10-03T09:00:00Z', movement: change > 0 ? CreditMovement.AdminAdd : CreditMovement.AdminDeduct, change, balanceAfter: credit.balance + change, invoiceId: null, description: reason },
      ...credit.entries.items,
    ],
    totalCount: credit.entries.totalCount + 1,
  },
})

/** The sentence the backend refuses a deduction with when the balance is short. */
export const insufficientSentence = 'The client does not have enough credit.'

/** The refusal body of a deduction larger than the balance. */
export const insufficientRefusal = { type: 'CLIENT_CREDIT_INSUFFICIENT', title: 'Bad Request', status: 400, detail: insufficientSentence }
