/**
 * A billable item and the paged envelope it arrives in.
 */

import type { PaginatedList } from '@/types/paginatedlist'

/**
 * A billable item that can be invoiced to a client, as one client's list shows it.
 *
 * Mirrors the API's `ClientBillableItemDto` (`GET /clients/{id}/billable-items`); the
 * admin-wide list adds the client's name ({@link BillableItem}).
 */
export interface ClientBillableItem {
  /** Unique billable item identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Charge description. */
  description: string
  /** Charge amount. */
  amount: number
  /** ISO 4217 currency code. */
  currency: string
  /** Item type: `OneTime` or `Recurring`. */
  type: string
  /** Recurrence period; null for one-time items. */
  recurringPeriod: string | null
  /** Whether the item has already been invoiced. */
  isInvoiced: boolean
  /** FK to the invoice; null while uninvoiced. */
  invoiceId: number | null
  /** ISO 8601 day the item is invoiced automatically (once for a one time item, next for a recurring one); null for none. */
  nextDueDate: string | null
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/**
 * A billable item in the admin-wide list: mirrors the API's `BillableItemDto`
 * (`GET /billing/billable-items`).
 */
export interface BillableItem extends ClientBillableItem {
  /** Full name of the owning client; every billable item has one, since its client cannot be deleted. */
  clientName: string
}

/** An amount with the currency it is in, as the API's `CurrencyAmountDto`. */
export interface CurrencyAmount {
  /** The amount. */
  amount: number
  /** ISO 4217 code of the currency the amount is in. */
  currency: string
}

/** Response from the client-scoped billable items endpoint. */
export interface BillableItemsResult {
  /** All uninvoiced items for the client. */
  uninvoicedItems: ClientBillableItem[]
  /** Sum of the uninvoiced item amounts, one entry per currency (empty when there are none). */
  uninvoicedTotal: CurrencyAmount[]
  /** Paginated invoiced items. */
  invoicedItems: PaginatedList<ClientBillableItem>
}

/**
 * Body of `POST /clients/{id}/billable-items`: the API's `CreateBillableItemCommand` without the
 * client, which the route supplies.
 */
export interface CreateClientBillableItemPayload {
  /** Charge description; required, at most `BILLABLE_ITEM_DESCRIPTION_MAX_LENGTH` (`lib/limits`) characters. */
  description: string
  /** Charge amount; zero or more. */
  amount: number
  /** ISO 4217 alpha currency code (three letters). */
  currency: string
  /** One time or recurring. */
  type: BillableItemType
  /** How often a recurring item repeats; required for, and only sent with, a recurring item. */
  recurringPeriod?: RecurringPeriod
  /**
   * ISO 8601 day the item is invoiced automatically: once for a one time item, next for a recurring
   * one. Left out, the item waits to be invoiced by hand.
   */
  nextDueDate?: string
}

/** Body of `POST /billing/billable-items`: the same command, naming the client in the body. */
export interface CreateBillableItemPayload extends CreateClientBillableItemPayload {
  /** The client the item is billed to. */
  clientId: number
}

/** A service offered in a billable-item dropdown. */
export interface ServiceOption {
  /** Service id. */
  value: number
  /** Product name shown to the operator. */
  label: string
}

/** Kinds of billable item the API knows (its `BillableItemType`, sent by name). */
export enum BillableItemType {
  /** Charged once. */
  OneTime = 'OneTime',
  /** Charged again every recurring period. */
  Recurring = 'Recurring',
}

/** How often a recurring billable item repeats, spelled as the API stores and reads it. */
export enum RecurringPeriod {
  /** Every week. */
  Weekly = 'weekly',
  /** Every month. */
  Monthly = 'monthly',
  /** Every three months. */
  Quarterly = 'quarterly',
  /** Every year. */
  Annually = 'annually',
}
