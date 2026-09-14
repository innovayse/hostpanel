/**
 * Pinia store for the operator's configured billing currencies and the current payer's
 * currency.
 *
 * Loads `GET /api/portal/public/billing-currencies` once and derives {@link payerCode}: the
 * signed-in client's own currency when there is one, else the guest's chosen currency
 * (persisted in localStorage, validated against the loaded list), else the base currency.
 * Nothing here converts an amount — every price on the storefront is a stored price in one of
 * these currencies, formatted with `utils/formatMoney.ts`.
 *
 * @module stores/currency
 */

import { defineStore } from 'pinia'
import { useCurrencyApi } from '~/composables/apis/useCurrencyApi'
import { useClientStore } from '~/stores/client'
import type { BillingCurrency } from '~/types/currency'
import type { MoneyCurrency } from '~/utils/formatMoney'

/** localStorage key the guest's chosen currency is persisted under. */
const STORAGE_KEY = 'innovayse_currency'

export const useCurrencyStore = defineStore('currency', {
  state: () => ({
    /** Every currency a visitor may be billed in, base first. Empty until {@link load}. */
    currencies: [] as BillingCurrency[],
    /** True once {@link currencies} reflects the server's answer. */
    loaded: false,
    /** True while the initial load is in flight. */
    loading: false,
    /** The guest's chosen currency code, or null when none has been chosen (yet). */
    selectedCode: null as string | null
  }),

  getters: {
    /** The base currency's code, or null before {@link load} resolves. */
    baseCode: (state): string | null =>
      state.currencies.find(c => c.isBase)?.code ?? null,

    /**
     * The currency the current visitor is billed in.
     *
     * Precedence: the signed-in client's own recorded currency, else the guest's chosen
     * currency (only if it is still a valid, loaded code), else the base currency. Mirrors
     * `PayerCurrencyResolver.ForCallerAsync` on the backend — a signed-in client's currency is
     * never overridden by a stale guest selection left over from before they signed in.
     */
    payerCode(state): string | null {
      const clientCurrency = useClientStore().user?.currency
      if (clientCurrency) return clientCurrency

      if (state.selectedCode && state.currencies.some(c => c.code === state.selectedCode)) {
        return state.selectedCode
      }

      return this.baseCode
    },

    /** The full {@link BillingCurrency} row for {@link payerCode}, or null before it resolves. */
    payerCurrency(): BillingCurrency | null {
      return this.currencyFor(this.payerCode)
    },

    /**
     * Looks up one currency's row by code.
     *
     * @returns A function taking a code and returning its row, or null when unknown.
     */
    currencyFor: (state) => (code: string | null | undefined): BillingCurrency | null =>
      state.currencies.find(c => c.code === code) ?? null,

    /**
     * Looks up one currency's row by code, shaped for `formatMoney`.
     *
     * Falls back to two decimals and no symbol rather than throwing, so a page that has not
     * yet loaded {@link currencies} — or is passed a code the list does not carry — still
     * renders a plain number instead of crashing.
     *
     * @returns A function taking a code and returning a {@link MoneyCurrency}.
     */
    moneyCurrencyFor: (state) => (code: string | null | undefined): MoneyCurrency => {
      const found = state.currencies.find(c => c.code === code)
      return found ?? { code: code ?? undefined, prefix: '', suffix: '', decimals: 2 }
    }
  },

  actions: {
    /**
     * Restores the guest's previously chosen currency from localStorage.
     * Must be called client-side only (e.g. in `onMounted`); validated once {@link load} has run.
     */
    init() {
      if (!import.meta.client) return
      try {
        this.selectedCode = localStorage.getItem(STORAGE_KEY)
      } catch {
        // ignore corrupt/blocked storage
      }
    },

    /**
     * Sets the guest's chosen currency and persists it.
     *
     * @param code - The currency code to select. Only meaningful for a guest — a signed-in
     * client's currency is fixed and this selection is ignored by {@link payerCode}.
     */
    select(code: string) {
      this.selectedCode = code
      if (!import.meta.client) return
      try {
        localStorage.setItem(STORAGE_KEY, code)
      } catch {
        // ignore quota/blocked storage
      }
    },

    /**
     * Loads the configured billing currencies from the server. No-ops if already loaded
     * unless `force` is true.
     *
     * @param force - Set true to bypass the loaded cache.
     * @returns Promise that resolves once {@link currencies} reflects the server's answer.
     */
    async load(force = false): Promise<void> {
      if (this.loaded && !force) return
      this.loading = true
      try {
        this.currencies = await useCurrencyApi().loadBillingCurrencies()
        this.loaded = true
      } catch {
        // Currencies failing to load is not fatal to the page rendering — formatMoney and the
        // getters above all degrade to a plain number rather than throwing.
      } finally {
        this.loading = false
      }
    }
  }
})
