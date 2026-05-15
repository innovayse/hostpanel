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
  /** WHMCS product ID */
  pid: number
  /** Human-readable plan name */
  name: string
  /** Billing cycle key, e.g. "monthly" */
  billingcycle: string
  /** Localised cycle label, e.g. "Monthly" */
  cycleLabel: string
  /** Formatted price string, e.g. "$9.99" */
  price: string
  /** Currency prefix, e.g. "$" */
  prefix: string
  /** Raw numeric price string, e.g. "9.99" */
  rawPrice: string
  /** Hosting specific: Domain name */
  domain?: string
  /** Hosting specific: Hostname (for VPS/Servers) */
  hostname?: string
  /** Hosting specific: Account username */
  username?: string
  /** Hosting specific: Account password */
  password?: string
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
      state.items.some(i => i.pid === pid && i.billingcycle === billingcycle)
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
     * If the same pid+billingcycle already exists, it is not duplicated.
     * Marks the pid as "recently added" for 2 seconds (button feedback).
     *
     * @param item - Cart item to add
     */
    addItem(item: CartItem) {
      if (!this.hasItem(item.pid, item.billingcycle)) {
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
     * Remove an item from the cart by its pid.
     *
     * @param pid - WHMCS product ID to remove
     */
    removeItem(pid: number) {
      this.items = this.items.filter(i => i.pid !== pid)
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
