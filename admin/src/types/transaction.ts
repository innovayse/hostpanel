/**
 * A payment transaction and the paged envelope it arrives in.
 */

import type { PaginatedList } from '@/types/paginatedlist'

/** Client-level financial ledger entry. */
export interface Transaction {
  /** Unique transaction identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client; every transaction has one, since its client cannot be deleted. */
  clientName: string
  /** ISO 8601 transaction date. */
  date: string
  /** Human-readable description of the transaction. */
  description: string
  /** External gateway transaction reference. */
  transactionId: string
  /** Optional linked invoice identifier. */
  invoiceId: number | null
  /** Payment method used (a gateway or a manual entry). */
  paymentMethod: string
  /** Amount received. */
  amountIn: number
  /** Amount sent out. */
  amountOut: number
  /** Gateway fees. */
  fees: number
  /** Whether this transaction affected client credit balance. */
  addedToCredit: boolean
  /** ISO 4217 code the amounts are in. */
  currency: string
}

/** Response from the transactions list endpoint with summary totals. */
export interface TransactionsResult {
  /** Paginated transaction list. */
  transactions: PaginatedList<Transaction>
  /** Sum of all AmountIn values. */
  totalIn: number
  /** Sum of all AmountOut values. */
  totalOut: number
  /** Sum of all Fees values. */
  totalFees: number
  /** Calculated balance: TotalIn - TotalOut - TotalFees. */
  balance: number
  /** ISO 4217 code of the base currency the four totals are expressed in. */
  currency: string
}

/** Fields every transaction body carries. */
export interface TransactionFields {
  /** ISO 8601 transaction date. */
  date: string
  /** Human-readable description. */
  description: string
  /** External gateway transaction reference. */
  transactionId: string
  /** Linked invoice identifier, if any. */
  invoiceId: number | null
  /** Payment method used. */
  paymentMethod: string
  /** Amount received. */
  amountIn: number
  /** Amount sent out. */
  amountOut: number
  /** Gateway fees. */
  fees: number
}

/** Body of `POST /transactions` from the billing ledger. */
export interface CreateTransactionPayload extends TransactionFields {
  /** The client the transaction belongs to. */
  clientId: number
  /** Whether the amount is added to the client credit balance. */
  addToCredit: boolean
}

/** Body of `POST /transactions` from a client profile. */
export interface CreateClientTransactionPayload extends TransactionFields {
  /** The client the transaction belongs to. */
  clientId: number
  /** Whether the amount is added to the client credit balance. */
  addedToCredit: boolean
}

/** Body of `PUT /transactions/{id}`. */
export type UpdateTransactionPayload = TransactionFields

/** What the add and edit transaction forms hold while the operator types. */
export interface TransactionFormValues {
  /** Transaction date (ISO day), null until picked. */
  date: string | null
  /** Id of the client the transaction belongs to, as the client picker holds it. */
  clientId: string
  /** External gateway transaction reference. */
  transactionId: string
  /** Payment method used. */
  paymentMethod: string
  /** What the transaction is for. */
  description: string
  /** Amount received. */
  amountIn: number
  /** Amount sent out. */
  amountOut: number
  /** Gateway fees. */
  fees: number
  /** Linked invoice ids, comma separated; only the first is sent. */
  invoiceIds: string
  /** Whether the amount is added to the client credit balance. */
  addToCredit: boolean
}

/** Closed set of payment methods a ledger transaction or domain can name, as the API stores them. */
export const PaymentMethod = {
  /** Paid by credit or debit card. */
  Card: 'Credit/Debit Card',
  /** Paid by bank transfer. */
  BankTransfer: 'Bank Transfer',
} as const

/** One of the payment methods. */
export type PaymentMethod = (typeof PaymentMethod)[keyof typeof PaymentMethod]
