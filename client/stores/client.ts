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
import { PortalErrorCode, apiErrorCode, apiErrorMessage } from '~/utils/apiError'
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

    // The API's own explanation of the flag above, kept because it is what the notice
    // renders. The backend resolves it from its resources in the language Accept-Language
    // asked for, so this is already Russian or Armenian where the caller is — the portal no
    // longer keeps a translation of its own for it. Null only when the refusal carried no
    // body at all, which means the request never reached the API.
    clientProfileMessage: null as string | null,

    // ── User ──────────────────────────────────────────────────────────────
    user: null as ClientUser | null,
    userLoading: false,
    userLoaded: false,
    userError: null as string | null,

    // The identity request currently on the wire, or null when none is. Not data — the
    // request itself — so that the several callers who all ask for the identity at once
    // share one round trip instead of racing. Vue leaves a Promise in reactive state
    // untouched (`reactive()` only proxies Object/Array/Map/Set), so this is a plain
    // reference, not a proxied thenable. See {@link useClientStore.fetchUser}.
    userInflight: null as Promise<void> | null,

    // Bumped by {@link useClientStore.reset}. An identity request captures it when it starts
    // and refuses to write its answer if it has moved on since — otherwise a sign-out during
    // a request in flight would be undone by that request landing a moment later, and the
    // next sign-in would open on the previous customer's name.
    userEpoch: 0,

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
     *
     * No-ops if already loaded unless `force` is true, and — the part `userLoaded` alone
     * cannot do — collapses *concurrent* callers onto a single request. Three layers ask for
     * the identity on one client-area page load: the `client-auth` plugin (through
     * {@link useAuthStore.fetchUser}, which delegates here), the `client` layout's
     * `onMounted`, and the page's own fetch. All three start before the first answer comes
     * back, so `userLoaded` is still false for every one of them and the flag stops nothing.
     * Holding the in-flight promise is what turns three round trips into one.
     *
     * What is held is the request, not a cached result: the promise is dropped as soon as it
     * settles. So the 404 a staff identity gets for having no client record is neither asked
     * for three times over nor remembered forever — a later navigation is free to ask again.
     *
     * @param force - Set true to bypass the loaded cache. A forced call also refuses to join
     * a request already in flight: it is asking for data newer than that request can carry.
     * @returns Promise that resolves once {@link user} reflects the server's answer.
     */
    async fetchUser(force = false): Promise<void> {
      if (this.userLoaded && !force) return
      if (this.userInflight && !force) return this.userInflight

      const epoch = this.userEpoch
      const request = (async (): Promise<void> => {
        this.userLoading = true
        this.userError = null
        try {
          const me = await useClientApi().fetchMe()
          if (this.userEpoch !== epoch) return
          this.user = me
          this.userLoaded = true
        } catch (err) {
          if (this.userEpoch !== epoch) return
          // "Not a customer account" is a state, not a fault — it gets its own flag so
          // nothing renders it in red, and the API's own sentence is kept beside the flag
          // because that sentence is the explanation the notice shows, already in the
          // caller's language. Anything else is kept too, not swallowed: the screens read it
          // to say the section failed instead of rendering it as empty.
          if (isClientProfileMissing(err)) {
            this.clientProfileMissing = true
            this.clientProfileMessage = apiErrorMessage(err)
          } else {
            this.userError = apiErrorMessage(err)
          }
        } finally {
          if (this.userEpoch === epoch) this.userLoading = false
        }
      })()

      this.userInflight = request
      try {
        await request
      } finally {
        // Only clear the slot if a forced call has not already claimed it for a newer
        // request — otherwise the older one finishing would hide the newer one from joiners.
        if (this.userInflight === request) this.userInflight = null
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
          this.clientProfileMessage = apiErrorMessage(err)
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
          this.clientProfileMessage = apiErrorMessage(err)
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
          this.clientProfileMessage = apiErrorMessage(err)
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
          this.clientProfileMessage = apiErrorMessage(err)
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
      this.clientProfileMessage = null
      this.user = null
      this.userLoaded = false
      this.userError = null
      // Dropped, not awaited: a sign-out must not leave the previous session's identity
      // request as something the next sign-in's callers can join and adopt the answer of.
      // Bumping the epoch disowns its answer too, for the case where it is already on the
      // wire and lands after this returns.
      this.userInflight = null
      this.userEpoch += 1
      this.userLoading = false
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
