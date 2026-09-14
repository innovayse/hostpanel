/**
 * Pinia store for the shopping cart.
 *
 * Persisted to localStorage so items survive page reloads.
 * SSR-safe — localStorage access is gated by `import.meta.client`.
 *
 * ## Currency is never converted here
 *
 * Every {@link CartItem} carries the `amount` and `currency` it was added in, both read
 * verbatim from the API at add time. This store used to hold a hardcoded `AMD_RATES` table and
 * convert every item's price into whatever currency the page's *language* implied — a page in
 * Russian billed in roubles regardless of what the visitor had actually been shown. Per
 * `docs/superpowers/specs/2026-09-13-multi-currency-pricing-design.md` §5, a page's language
 * says nothing about money: the payer's currency comes from `stores/currency.ts`, and the total
 * is a sum, not a conversion.
 *
 * @module stores/cart
 */

import { defineStore } from 'pinia'
import type { CartItem } from '~/types/cartitem'

const STORAGE_KEY = 'innovayse_cart'

/**
 * True when a persisted item still has the pre-multi-currency shape (`price`/`prefix`/
 * `rawPrice`/`priceAmd` instead of `amount`/`currency`).
 *
 * A cart loaded from an old `localStorage` entry has no reliable amount to show any more — the
 * old fields were formatted strings or AMD-only figures, never a currency-tagged number — so
 * such items are dropped on load rather than guessed at. See {@link useCartStore.init}.
 *
 * @param item - The parsed localStorage entry.
 * @returns True when the item predates `amount`/`currency` and must be dropped.
 */
function isLegacyItem(item: unknown): boolean {
  return typeof item !== 'object' || item === null ||
    typeof (item as { amount?: unknown }).amount !== 'number' ||
    typeof (item as { currency?: unknown }).currency !== 'string'
}

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
      state.items.some(i => i.itemType === 'domain' && i.domain === domain && i.domainAction === action),

    /**
     * Items priced in `payerCode`, ready to sum and display.
     *
     * @returns A function taking the payer's current currency code and returning the items
     * still valid to charge in it.
     */
    itemsInCurrency: (state) => (payerCode: string | null): CartItem[] =>
      payerCode ? state.items.filter(i => i.currency === payerCode) : state.items,

    /**
     * Items priced in some other currency than `payerCode` — added before the payer changed
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
     * Items persisted before multi-currency pricing (no `amount`/`currency`) are dropped —
     * see {@link isLegacyItem} — since their stored price can no longer be trusted or shown.
     */
    init() {
      if (!import.meta.client) return
      try {
        const saved = localStorage.getItem(STORAGE_KEY)
        if (!saved) return
        const parsed = JSON.parse(saved) as unknown[]
        this.items = parsed.filter(i => !isLegacyItem(i)) as CartItem[]
        if (this.items.length !== parsed.length) this._save()
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
