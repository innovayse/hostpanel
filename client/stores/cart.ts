/**
 * Pinia store for the shopping cart.
 *
 * Persisted to localStorage so items survive page reloads.
 * SSR-safe: localStorage access is gated by `import.meta.client`.
 *
 * ## Currency is never converted here
 *
 * Every {@link CartItem} carries the `amount` and `currency` it was added in, both read
 * verbatim from the API at add time. A page's language says nothing about money: the payer's
 * currency comes from `stores/currency.ts`, and the total is a sum, not a conversion, so a
 * visitor is billed in the currency they were shown.
 *
 * @module stores/cart
 */

import { defineStore } from 'pinia'
import { RECENTLY_ADDED_MS } from '~/constants/uiDefaults'
import type { CartItem } from '~/types/cartitem'
import { CartItemType } from '~/types/cartitemtype'
import { ProductType } from '~/types/producttype'
import { DomainOrderAction } from '~/types/domainorderaction'
import type { PortalProduct } from '~/types/portalproduct'
import { PricingCycle } from '~/types/pricingcycle'
import { cycleAmount } from '~/utils/productFamilies'

/** localStorage key the cart is persisted under. */
const STORAGE_KEY = 'innovayse_cart'

/**
 * True when `value` is one of the members of the string enum `values`.
 *
 * @param values - The enum's member values.
 * @param value - The value read from storage.
 * @returns True when the value is a member.
 */
const isMember = (values: readonly string[], value: unknown): boolean => {
  return typeof value === 'string' && values.includes(value)
}

/**
 * True when an optional field is absent or has the expected `typeof`.
 *
 * @param value - The field value read from storage.
 * @param type - The expected `typeof` result.
 * @returns True when the field is missing or of that type.
 */
const isOptional = (value: unknown, type: 'string' | 'number'): boolean => {
  return value === undefined || typeof value === type
}

/**
 * True when a parsed localStorage entry has the full {@link CartItem} shape.
 *
 * localStorage is untrusted input: anything a visitor, an extension or another build wrote
 * there may be malformed, so only entries matching the current shape (numeric `pid`/`amount`,
 * string `name`/`cycleLabel`/`currency`, a {@link PricingCycle} cycle and valid optional
 * fields) are loaded. See {@link useCartStore.init}.
 *
 * @param item - The parsed localStorage entry.
 * @returns True when the entry can be used as a cart item.
 */
const isValidItem = (item: unknown): item is CartItem => {
  if (typeof item !== 'object' || item === null) {
    return false
  }
  const i = item as Record<string, unknown>
  // A line never carries account credentials; a stored entry that does is dropped rather than
  // kept in the browser.
  return !('username' in i) && !('password' in i) &&
    typeof i.pid === 'number' && Number.isFinite(i.pid) &&
    typeof i.name === 'string' &&
    isMember(Object.values(PricingCycle), i.billingcycle) &&
    typeof i.cycleLabel === 'string' &&
    typeof i.amount === 'number' && Number.isFinite(i.amount) &&
    typeof i.currency === 'string' &&
    isOptional(i.domain, 'string') &&
    isOptional(i.hostname, 'string') &&
    (i.itemType === undefined || isMember(Object.values(CartItemType), i.itemType)) &&
    (i.domainAction === undefined || isMember(Object.values(DomainOrderAction), i.domainAction)) &&
    isOptional(i.eppCode, 'string') &&
    isOptional(i.tld, 'string') &&
    isOptional(i.years, 'number') &&
    // Absent on a line saved before the field existed: still a valid line.
    (i.productType === undefined || isMember(Object.values(ProductType), i.productType))
}

// ---------------------------------------------------------------------------
// Store
// ---------------------------------------------------------------------------

/**
 * Central store for the shopping cart: its items, their currency-aware totals and persistence.
 *
 * @returns The cart state and the actions that change it.
 */
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
    hasItem: (state) => (pid: number, billingcycle: PricingCycle): boolean =>
      state.items.some(i => i.pid === pid && i.billingcycle === billingcycle),

    /** Check if a domain + action combo is already in cart */
    hasDomainItem: (state) => (domain: string, action: DomainOrderAction): boolean =>
      state.items.some(i => i.itemType === CartItemType.Domain && i.domain === domain && i.domainAction === action),

    /**
     * Items priced in `payerCode`, ready to sum and display.
     *
     * @returns A function taking the payer's current currency code and returning the items
     * still valid to charge in it.
     */
    itemsInCurrency: (state) => (payerCode: string | null): CartItem[] =>
      payerCode ? state.items.filter(i => i.currency === payerCode) : state.items,

    /**
     * Whether everything the payer will be charged for is a digital good: the order then has no
     * hosting service to set up, so the confirmation offers no setup. A line without a known type
     * counts as not digital, and an empty set is not "only digital".
     *
     * @returns A function taking the payer's current currency code and answering for the items
     * priced in it.
     */
    onlyDigital: (state) => (payerCode: string | null): boolean => {
      const ordered = payerCode ? state.items.filter(i => i.currency === payerCode) : state.items
      return ordered.length > 0 && ordered.every(i => i.productType === ProductType.Digital)
    },

    /**
     * Items priced in some other currency than `payerCode`: added before the payer changed
     * currency. Excluded from {@link total} and shown with a "remove and re-add" notice rather
     * than reprised, because nothing on the frontend is allowed to convert a stored price.
     *
     * @returns A function taking the payer's current currency code and returning the stale items.
     */
    staleItems: (state) => (payerCode: string | null): CartItem[] =>
      payerCode ? state.items.filter(i => i.currency !== payerCode) : [],

    /**
     * Sum of {@link CartItem.amount} for items priced in `payerCode`.
     *
     * @returns A function taking the payer's current currency code and returning the total.
     */
    total: (state) => (payerCode: string | null): number =>
      state.items
        .filter(i => !payerCode || i.currency === payerCode)
        .reduce((sum, i) => sum + i.amount, 0)
  },

  actions: {
    /**
     * Load cart from localStorage.
     * Must be called on the client side only (e.g. in onMounted).
     *
     * Storage is untrusted input: entries without a valid {@link CartItem} shape are dropped
     * (see {@link isValidItem}) and the cleaned cart is saved back.
     */
    init() {
      if (!import.meta.client) {
        return
      }
      try {
        const saved = localStorage.getItem(STORAGE_KEY)
        if (!saved) {
          return
        }
        const parsed: unknown = JSON.parse(saved)
        const entries = Array.isArray(parsed) ? parsed : []
        this.items = entries.filter(isValidItem)
        if (this.items.length !== entries.length || !Array.isArray(parsed)) {
          this._save()
        }
      } catch {
        // ignore corrupt storage
      }
    },

    /** Persist current items to localStorage */
    _save() {
      if (!import.meta.client) {
        return
      }
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
      const isDuplicate = item.itemType === CartItemType.Domain && item.domain !== undefined && item.domainAction !== undefined
        ? this.hasDomainItem(item.domain, item.domainAction)
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
        }, RECENTLY_ADDED_MS)
      }
    },

    /**
     * Remove an item from the cart.
     * For domain items, removes by domain+action. For hosting, removes the line with that pid and
     * billing cycle only: the same plan on the other cycle is a different line and stays.
     *
     * @param pid - Product ID to remove
     * @param billingcycle - Billing cycle of the line to remove
     * @param domain - Domain name (for domain items)
     * @param domainAction - Domain action (for domain items)
     */
    removeItem(pid: number, billingcycle: PricingCycle, domain?: string, domainAction?: DomainOrderAction) {
      if (domain && domainAction) {
        this.items = this.items.filter(
          i => !(i.domain === domain && i.domainAction === domainAction))
      } else {
        this.items = this.items.filter(i => !(i.pid === pid && i.billingcycle === billingcycle))
      }
      this._save()
    },

    /**
     * Re-prices the hosting lines from the catalogue so a price that changed since the item was added
     * is shown before the payer pays it. A line whose product is gone, or carries no price for its
     * cycle, keeps its stored price (the order is then priced and confirmed by the backend). Domain
     * lines are priced from the TLD table and lines in another currency are stale; neither is touched.
     *
     * @param products - The catalogue products, priced in the payer's currency.
     * @param payerCode - The payer's currency code the catalogue was read in.
     */
    refreshPrices(products: readonly PortalProduct[], payerCode: string) {
      let changed = false
      this.items = this.items.map((item) => {
        // A line in another currency is flagged stale for the payer to re-add, never re-priced here.
        if (item.itemType === CartItemType.Domain || item.currency !== payerCode) {
          return item
        }
        const amount = cycleAmount(products.find(p => p.id === item.pid)?.pricing, item.billingcycle)
        if (amount === null || amount === item.amount) {
          return item
        }
        changed = true
        return { ...item, amount }
      })
      if (changed) {
        this._save()
      }
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
