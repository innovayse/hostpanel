/**
 * The TLD price book: one row, its list shape, the payloads that write it, and what an
 * import reports back.
 */

/** TLD configuration for pricing management. */
export interface TldConfig {
  /** Unique identifier. */
  id: number
  /** Top-level domain suffix (e.g. "com", "net"). */
  tld: string
  /** Registrar module name (e.g. "NameAm", "Namecheap"). */
  registrarModule: string
  /** ISO 4217 cost currency code. */
  currency: string
  /** ISO 4217 sell currency code. */
  sellCurrency: string
  /** Whether this TLD is available for sale. */
  isEnabled: boolean
  /** Display order in pricing lists. */
  sortOrder: number
  /** TLD category tags. */
  categories: string[]
  /** Cost prices for registration keyed by period (e.g. "1", "2"). */
  costRegister: Record<string, number>
  /** Cost prices for transfer keyed by period. */
  costTransfer: Record<string, number>
  /** Cost prices for renewal keyed by period. */
  costRenew: Record<string, number>
  /** Sell prices for registration keyed by period. */
  sellRegister: Record<string, number>
  /** Sell prices for transfer keyed by period. */
  sellTransfer: Record<string, number>
  /** Sell prices for renewal keyed by period. */
  sellRenew: Record<string, number>
  /** ISO 8601 timestamp of the last price sync, or null if never synced. */
  lastSyncedAt: string | null
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Summary TLD config for list views. */
export interface TldConfigListItem {
  /** Unique identifier. */
  id: number
  /** Top-level domain suffix. */
  tld: string
  /** Registrar module name. */
  registrarModule: string
  /** Whether this TLD is available for sale. */
  isEnabled: boolean
  /** 1-year registration cost price, or null if not set. */
  costRegister1yr: number | null
  /** 1-year registration sell price, or null if not set. */
  sellRegister1yr: number | null
  /** Margin percentage between cost and sell, or null if not calculable. */
  marginPercent: number | null
  /** ISO 4217 cost currency code. */
  currency: string
  /** ISO 4217 sell currency code. */
  sellCurrency: string
  /** TLD category tags. */
  categories: string[]
  /** ISO 8601 timestamp of the last price sync, or null. */
  lastSyncedAt: string | null
  /** Display order. */
  sortOrder: number
}

/** Payload for creating a new TLD config. */
export interface CreateTldConfigPayload {
  /** Top-level domain suffix. */
  tld: string
  /** Registrar module name. */
  registrarModule: string
  /** ISO 4217 cost currency code. */
  currency: string
  /** ISO 4217 sell currency code. */
  sellCurrency: string
  /** Whether this TLD is available for sale. */
  isEnabled: boolean
  /** Display order. */
  sortOrder: number
  /** TLD category tags. */
  categories: string[]
  /** Cost prices for registration keyed by period. */
  costRegister: Record<string, number>
  /** Cost prices for transfer keyed by period. */
  costTransfer: Record<string, number>
  /** Cost prices for renewal keyed by period. */
  costRenew: Record<string, number>
  /** Sell prices for registration keyed by period. */
  sellRegister: Record<string, number>
  /** Sell prices for transfer keyed by period. */
  sellTransfer: Record<string, number>
  /** Sell prices for renewal keyed by period. */
  sellRenew: Record<string, number>
}

/** Payload for updating an existing TLD config. */
export interface UpdateTldConfigPayload extends CreateTldConfigPayload {
  /** TLD config identifier. */
  id: number
}

/** Result of bulk TLD import from a registrar provider. */
export interface TldImportResult {
  /** Number of newly imported TLD configs. */
  imported: number
  /** Number of existing TLD configs that were updated. */
  updated: number
}
