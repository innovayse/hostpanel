/**
 * One function per endpoint on the API transaction surface (`/api/transactions`), both the
 * billing ledger and the per-client view. No state lives here, no error handling.
 *
 * Callers are the two transaction stores, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type {
  CreateClientTransactionPayload,
  CreateTransactionPayload,
  Transaction,
  TransactionFilters,
  TransactionsResult,
  UpdateTransactionPayload,
} from '@/types/transaction'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for transactions.
 *
 * @returns One typed function per transaction endpoint the admin panel uses.
 */
export const useTransactionsApi = () => {
  const { request } = useApi()

  /**
   * Lists transactions across all clients, with summary totals.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param filters - The search text and the client, direction, payment method, period and amount filters; a member left out is not sent.
   * @returns One page of transactions and the totals of the rows the filters keep.
   */
  const list = (page: number, pageSize: number, filters: TransactionFilters = {}): Promise<TransactionsResult> =>
    request<TransactionsResult>(`${ApiPath.Transactions}${buildQuery({ page, pageSize, ...filters })}`)

  /**
   * Lists the transactions of one client, with summary totals.
   *
   * @param clientId - The client id.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param filters - The search text and the direction, payment method, period and amount filters; a member left out is not sent.
   * @returns One page of transactions and the totals of the rows the filters keep.
   */
  const listByClient = (
    clientId: number | string,
    page: number,
    pageSize: number,
    filters: TransactionFilters = {},
  ): Promise<TransactionsResult> =>
    request<TransactionsResult>(`${ApiPath.Transactions}/client/${clientId}${buildQuery({ page, pageSize, ...filters })}`)

  /**
   * Loads one transaction.
   *
   * @param id - The transaction id.
   * @returns The transaction.
   */
  const get = (id: number): Promise<Transaction> => request<Transaction>(`${ApiPath.Transactions}/${id}`)

  /**
   * Creates a transaction.
   *
   * @param payload - Transaction fields.
   * @returns Nothing.
   */
  const create = (payload: CreateTransactionPayload | CreateClientTransactionPayload): Promise<void> =>
    request<void>(ApiPath.Transactions, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Updates a transaction.
   *
   * @param id - The transaction id.
   * @param payload - Transaction fields.
   * @returns Nothing.
   */
  const update = (id: number, payload: UpdateTransactionPayload): Promise<void> =>
    request<void>(`${ApiPath.Transactions}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a transaction.
   *
   * @param id - The transaction id.
   * @returns Nothing.
   */
  const remove = (id: number): Promise<void> => request<void>(`${ApiPath.Transactions}/${id}`, { method: HttpMethod.Delete })

  return { list, listByClient, get, create, update, remove }
}
