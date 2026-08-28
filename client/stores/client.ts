/**
 * Pinia store for the WHMCS client area.
 *
 * Used **only** on `/client/*` pages — these pages are behind authentication
 * and do not require SSR / SEO, so a client-side store is appropriate.
 *
 * Public pages (/hosting, /domains, etc.) use `useFetch` / `useApi` directly
 * to preserve SSR and search-engine indexability, through their API composable rather than a
 * store. `nuxt.config.ts` makes the split concrete: `routeRules` sets `ssr: false` and
 * `X-Robots-Tag: noindex` for `/client/**`, `/cart/**` and `/checkout/**`, and nothing else.
 * A deliberate, documented exception to component -> store -> api, not an oversight.
 *
 * URLs and transport belong to {@link useClientApi}; this store owns only the state, the
 * loaded/loading flags and how a failure is reported.
 *
 * @module stores/client
 */

import { defineStore } from 'pinia'
import { useClientApi } from '~/composables/apis/useClientApi'
import { PortalErrorCode, apiErrorCode, apiErrorMessage } from '~/utils/portalErrorMessages'
import type { ClientUser } from '~/types/clientuser'
import type { ClientService } from '~/types/clientservice'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { ClientDomain } from '~/types/clientdomain'
import type { ClientTicket } from '~/types/clientticket'

/**
 * True when the API answered that the signed-in identity has no client profile at all.
 *
 * Recognised by the backend's `code`, never by its sentence: the sentence is free to be
 * reworded or translated, and a branch that matched on English prose would go quiet the day
 * it was.
 *
 * @param err - Whatever the API composable threw.
 * @returns True when this is the "not a customer account" answer rather than a failure.
 */
function isClientProfileMissing(err: unknown): boolean {
  return apiErrorCode(err) === PortalErrorCode.ClientProfileNotFound
}

// ---------------------------------------------------------------------------
// Store
// ---------------------------------------------------------------------------

/**
 * Central store for all WHMCS client area data.
 *
 * Each section (user, services, invoices, domains, tickets) has:
 * - A state array / object
 * - A `loading` flag
 * - A `fetch` action that calls the corresponding API endpoint
 * - A `loaded` flag so we don't re-fetch on every page visit
 */
export const useClientStore = defineStore('client', {
  state: () => ({
    // Every section carries an error beside its list. Without one, a failed fetch left the
    // list empty and the screens above read that as "you have nothing yet" — the dashboard
    // showed 0/0/0/0 and "no products/services with us yet" to an account whose four calls
    // had all answered 400. A failure and an empty account must not look the same.

    // Set instead of the per-section errors below when the API says this account has no
    // client profile. It is one account-wide fact, not five section failures: the screens
    // render one explanation with a way out, rather than the same red alert four times over.
    clientProfileMissing: false,

    // ── User ──────────────────────────────────────────────────────────────
    user: null as ClientUser | null,
    userLoading: false,
    userLoaded: false,
    userError: null as string | null,

    // ── Services ──────────────────────────────────────────────────────────
    services: [] as ClientService[],
    servicesLoading: false,
    servicesLoaded: false,
    servicesError: null as string | null,

    // ── Invoices ──────────────────────────────────────────────────────────
    invoices: [] as ClientInvoice[],
    invoicesLoading: false,
    invoicesLoaded: false,
    invoicesError: null as string | null,

    // ── Domains ───────────────────────────────────────────────────────────
    domains: [] as ClientDomain[],
    domainsLoading: false,
    domainsLoaded: false,
    domainsError: null as string | null,

    // ── Tickets ───────────────────────────────────────────────────────────
    tickets: [] as ClientTicket[],
    ticketsLoading: false,
    ticketsLoaded: false,
    ticketsError: null as string | null
  }),

  getters: {
    /** Full name from first + last name fields */
    fullName: (state): string =>
      state.user
        ? `${state.user.firstname} ${state.user.lastname}`.trim()
        : '',

    /** First letter of name for avatar */
    userInitial: (state): string => {
      if (!state.user) return '?'
      const name = `${state.user.firstname} ${state.user.lastname}`.trim()
      return (name || state.user.email || '?').charAt(0).toUpperCase()
    },

    /** Count of unpaid invoices */
    unpaidCount: (state): number =>
      state.invoices.filter(i => i.status === 'Unpaid').length,

    /** Count of active services */
    activeServiceCount: (state): number =>
      state.services.filter(s => s.status === 'Active').length,

    /** Count of open tickets */
    openTicketCount: (state): number =>
      state.tickets.filter(t => t.status === 'Open').length
  },

  actions: {
    // ── User ──────────────────────────────────────────────────────────────

    /**
     * Fetch the authenticated client's profile from WHMCS.
     * No-ops if already loaded unless `force` is true.
     *
     * @param force - Set true to bypass the loaded cache
     */
    async fetchUser(force = false) {
      if (this.userLoaded && !force) return
      this.userLoading = true
      this.userError = null
      try {
        this.user = await useClientApi().fetchMe()
        this.userLoaded = true
      } catch (err) {
        // "Not a customer account" is a state, not a fault — it gets its own flag and no
        // error string, so nothing renders it in red. Anything else is kept, not swallowed:
        // the screens read it to say the section failed instead of rendering it as empty.
        if (isClientProfileMissing(err)) {
          this.clientProfileMissing = true
        } else {
          this.userError = apiErrorMessage(err)
        }
      } finally {
        this.userLoading = false
      }
    },

    // ── Services ──────────────────────────────────────────────────────────

    /**
     * Fetch the client's hosting services.
     * No-ops if already loaded unless `force` is true.
     *
     * @param force - Set true to bypass the loaded cache
     */
    async fetchServices(force = false) {
      if (this.servicesLoaded && !force) return
      this.servicesLoading = true
      this.servicesError = null
      try {
        this.services = await useClientApi().fetchServices()
        this.servicesLoaded = true
      } catch (err) {
        // "Not a customer account" is a state, not a fault — it gets its own flag and no
        // error string, so nothing renders it in red. Anything else is kept, not swallowed:
        // the screens read it to say the section failed instead of rendering it as empty.
        if (isClientProfileMissing(err)) {
          this.clientProfileMissing = true
        } else {
          this.servicesError = apiErrorMessage(err)
        }
      } finally {
        this.servicesLoading = false
      }
    },

    // ── Invoices ──────────────────────────────────────────────────────────

    /**
     * Fetch the client's invoices.
     * No-ops if already loaded unless `force` is true.
     *
     * @param force - Set true to bypass the loaded cache
     */
    async fetchInvoices(force = false) {
      if (this.invoicesLoaded && !force) return
      this.invoicesLoading = true
      this.invoicesError = null
      try {
        this.invoices = await useClientApi().fetchInvoices()
        this.invoicesLoaded = true
      } catch (err) {
        // "Not a customer account" is a state, not a fault — it gets its own flag and no
        // error string, so nothing renders it in red. Anything else is kept, not swallowed:
        // the screens read it to say the section failed instead of rendering it as empty.
        if (isClientProfileMissing(err)) {
          this.clientProfileMissing = true
        } else {
          this.invoicesError = apiErrorMessage(err)
        }
      } finally {
        this.invoicesLoading = false
      }
    },

    // ── Domains ───────────────────────────────────────────────────────────

    /**
     * Fetch the client's domains.
     * No-ops if already loaded unless `force` is true.
     *
     * @param force - Set true to bypass the loaded cache
     */
    async fetchDomains(force = false) {
      if (this.domainsLoaded && !force) return
      this.domainsLoading = true
      this.domainsError = null
      try {
        this.domains = await useClientApi().fetchDomains()
        this.domainsLoaded = true
      } catch (err) {
        // "Not a customer account" is a state, not a fault — it gets its own flag and no
        // error string, so nothing renders it in red. Anything else is kept, not swallowed:
        // the screens read it to say the section failed instead of rendering it as empty.
        if (isClientProfileMissing(err)) {
          this.clientProfileMissing = true
        } else {
          this.domainsError = apiErrorMessage(err)
        }
      } finally {
        this.domainsLoading = false
      }
    },

    // ── Tickets ───────────────────────────────────────────────────────────

    /**
     * Fetch the client's support tickets.
     * No-ops if already loaded unless `force` is true.
     *
     * @param force - Set true to bypass the loaded cache
     */
    async fetchTickets(force = false) {
      if (this.ticketsLoaded && !force) return
      this.ticketsLoading = true
      this.ticketsError = null
      try {
        this.tickets = await useClientApi().fetchTickets()
        this.ticketsLoaded = true
      } catch (err) {
        // "Not a customer account" is a state, not a fault — it gets its own flag and no
        // error string, so nothing renders it in red. Anything else is kept, not swallowed:
        // the screens read it to say the section failed instead of rendering it as empty.
        if (isClientProfileMissing(err)) {
          this.clientProfileMissing = true
        } else {
          this.ticketsError = apiErrorMessage(err)
        }
      } finally {
        this.ticketsLoading = false
      }
    },

    /**
     * Fetch all client data in parallel (for the dashboard).
     * Skips sections already loaded unless `force` is true.
     *
     * @param force - Set true to refresh all sections
     */
    async fetchAll(force = false) {
      await Promise.all([
        this.fetchUser(force),
        this.fetchServices(force),
        this.fetchInvoices(force),
        this.fetchDomains(force),
        this.fetchTickets(force)
      ])
    },

    /**
     * Clear all client data on logout.
     */
    reset() {
      this.clientProfileMissing = false
      this.user = null
      this.userLoaded = false
      this.userError = null
      this.services = []
      this.servicesLoaded = false
      this.servicesError = null
      this.invoices = []
      this.invoicesLoaded = false
      this.invoicesError = null
      this.domains = []
      this.domainsLoaded = false
      this.domainsError = null
      this.tickets = []
      this.ticketsLoaded = false
      this.ticketsError = null
    }
  }
})
