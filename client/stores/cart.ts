/**
 * Pinia store for the shopping cart.
 *
 * Persisted to localStorage so items survive page reloads.
 * SSR-safe — localStorage access is gated by `import.meta.client`.
 *
 * @module stores/cart
 */

import { defineStore } from 'pinia'

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** A single product in the cart */
export interface CartItem {
  /** Product ID (generic domain product PID for domain items) */
  pid: number
  /** Human-readable plan name or domain name */
  name: string
  /** Billing cycle key, e.g. "monthly", "annually" */
  billingcycle: string
  /** Localised cycle label, e.g. "Monthly", "1 Year" */
  cycleLabel: string
  /** Formatted price string, e.g. "$9.99" (used for hosting items) */
  price: string
  /** Currency prefix, e.g. "$" (used for hosting items) */
  prefix: string
  /** Raw numeric price string, e.g. "9.99" (used for hosting items) */
  rawPrice: string
  /** Base price in AMD for domain items — converted to display currency at render time */
  priceAmd?: number
  /** Domain name for hosting or domain items */
  domain?: string
  /** Hostname for VPS/server items */
  hostname?: string
  /** Account username for hosting items */
  username?: string
  /** Account password for hosting items */
  password?: string
  /** Item type: "hosting" or "domain". Defaults to "hosting" if omitted. */
  itemType?: 'hosting' | 'domain'
  /** Domain action: "register" or "transfer" */
  domainAction?: 'register' | 'transfer'
  /** EPP/auth code for domain transfers */
  eppCode?: string
  /** TLD extension, e.g. "com", "net" */
  tld?: string
  /** Registration/transfer period in years */
  years?: number
}

/** Exchange rates: 1 AMD = X target currency */
const AMD_RATES: Record<string, number> = {
  AMD: 1,
  USD: 1 / 390,
  EUR: 1 / 420,
  RUB: 1 / 4.5,
  GBP: 1 / 490,
}

/** Currency symbols keyed by code */
const CURRENCY_SYMBOLS: Record<string, string> = {
  AMD: '֏',
  USD: '$',
  EUR: '€',
  RUB: '₽',
  GBP: '£',
}

/**
 * Converts an AMD price to the target currency.
 *
 * @param amountAmd - Price in Armenian drams.
 * @param targetCurrency - ISO 4217 currency code.
 * @returns Converted numeric amount.
 */
export function convertFromAmd(amountAmd: number, targetCurrency: string): number {
  const rate = AMD_RATES[targetCurrency] ?? AMD_RATES['USD']!
  return amountAmd * rate
}

/**
 * Formats a cart item price for display based on the target currency.
 *
 * @param item - The cart item.
 * @param targetCurrency - ISO 4217 currency code to display in.
 * @returns Formatted price string, e.g. "$22.82 USD".
 */
export function formatCartItemPrice(item: CartItem, targetCurrency: string): string {
  if (item.priceAmd !== undefined) {
    const converted = convertFromAmd(item.priceAmd, targetCurrency)
    const symbol = CURRENCY_SYMBOLS[targetCurrency] ?? '$'
    return `${symbol}${converted.toFixed(2)} ${targetCurrency}`
  }
  return item.price
}

const STORAGE_KEY = 'innovayse_cart'

// ---------------------------------------------------------------------------
// Store
// ---------------------------------------------------------------------------

export const useCartStore = defineStore('cart', {
  state: () => ({
    items: [] as CartItem[],
    /** Tracks which pids were just added (for brief success animation) */
    recentlyAdded: [] as number[],
    /** Whether the cart drawer is open */
    isOpen: false
  }),

  getters: {
    /** Total number of items in cart */
    count: (state): number => state.items.length,

    /** Whether the cart has any items */
    isEmpty: (state): boolean => state.items.length === 0,

    /** Check if a specific pid+cycle combo is already in cart */
    hasItem: (state) => (pid: number, billingcycle: string): boolean =>
      state.items.some(i => i.pid === pid && i.billingcycle === billingcycle),

    /** Check if a domain + action combo is already in cart */
    hasDomainItem: (state) => (domain: string, action: string): boolean =>
      state.items.some(i => i.itemType === 'domain' && i.domain === domain && i.domainAction === action)
  },

  actions: {
    /**
     * Load cart from localStorage.
     * Must be called on the client side only (e.g. in onMounted).
     */
    init() {
      if (!import.meta.client) return
      try {
        const saved = localStorage.getItem(STORAGE_KEY)
        if (saved) this.items = JSON.parse(saved) as CartItem[]
      } catch {
        // ignore corrupt storage
      }
    },

    /** Persist current items to localStorage */
    _save() {
      if (!import.meta.client) return
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(this.items))
      } catch {
        // ignore quota errors
      }
    },

    /**
     * Add an item to the cart.
     * For hosting: deduplicates by pid+billingcycle.
     * For domains: deduplicates by domain+domainAction.
     *
     * @param item - Cart item to add
     */
    addItem(item: CartItem) {
      const isDuplicate = item.itemType === 'domain'
        ? this.hasDomainItem(item.domain!, item.domainAction!)
        : this.hasItem(item.pid, item.billingcycle)

      if (!isDuplicate) {
        this.items.push(item)
        this._save()
      }
      
      // Automatically open drawer when item is added
      this.open()

      // brief feedback flag
      if (!this.recentlyAdded.includes(item.pid)) {
        this.recentlyAdded.push(item.pid)
        setTimeout(() => {
          this.recentlyAdded = this.recentlyAdded.filter(p => p !== item.pid)
        }, 2000)
      }
    },

    /**
     * Remove an item from the cart.
     * For domain items, removes by domain+action. For hosting, removes by pid.
     *
     * @param pid - Product ID to remove
     * @param domain - Domain name (for domain items)
     * @param domainAction - Domain action (for domain items)
     */
    removeItem(pid: number, domain?: string, domainAction?: string) {
      if (domain && domainAction) {
        this.items = this.items.filter(
          i => !(i.domain === domain && i.domainAction === domainAction))
      } else {
        this.items = this.items.filter(i => i.pid !== pid)
      }
      this._save()
    },

    /**
     * Remove all items from the cart.
     */
    clear() {
      this.items = []
      this._save()
    },

    /** Open the cart drawer */
    open() {
      this.isOpen = true
    },

    /** Close the cart drawer */
    close() {
      this.isOpen = false
    },

    /** Toggle the cart drawer */
    toggle() {
      this.isOpen = !this.isOpen
    }
  }
})
