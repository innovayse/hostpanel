/**
 * Pinia store for the WHMCS client area.
 *
 * Used **only** on `/client/*` pages — these pages are behind authentication
 * and do not require SSR / SEO, so a client-side store is appropriate.
 *
 * Public pages (/hosting, /domains, etc.) use `useFetch` / `useApi` directly
 * to preserve SSR and search-engine indexability.
 *
 * @module stores/client
 */

import { defineStore } from 'pinia'
import { apiFetch } from '~/composables/useApi'

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Authenticated user info returned by /api/portal/client/me */
export interface ClientUser {
  id: number
  firstname: string
  lastname: string
  companyname?: string
  email: string
  phonenumber?: string
  address1?: string
  address2?: string
  city?: string
  state?: string
  postcode?: string
  /** ISO 3166-1 alpha-2 country code, e.g. "AM" */
  country?: string
  /** Full country name, e.g. "Armenia" */
  countryname?: string
  /** Default payment gateway module name (e.g. "paypal", "stripe") */
  defaultgateway?: string
  /** WHMCS language preference (e.g. "english", "russian") */
  language?: string
  /** Per-category email opt-in flags — 1 = receives emails, 0 = opted out */
  email_preferences?: {
    general: 0 | 1
    invoice: 0 | 1
    support: 0 | 1
    product: 0 | 1
    domain: 0 | 1
    affiliate: 0 | 1
  }
  currency?: number
  currencyprefix?: string
  currencysuffix?: string
  /** User permissions as bit-flags integer (8191 = All). */
  permissions: number
}

/** A hosting service from GetClientsProducts */
export interface ClientService {
  id: number
  clientid: number
  pid: number
  regdate: string
  name: string
  translated_name?: string
  groupname: string
  domain: string
  dedicatedip: string
  serverid: number
  servername: string
  serverip: string
  serverhostname: string
  suspensionreason: string
  firstpaymentamount: string
  recurringamount: string
  paymentmethod: string
  paymentmethodname: string
  billingcycle: string
  nextduedate: string
  status: string
  username: string
  diskusage: string
  disklimit: string
  bwusage: string
  bwlimit: string
  lastupdate: string
}

/** An invoice from GetInvoices */
export interface ClientInvoice {
  id: number
  userid: number
  date: string
  duedate: string
  datepaid: string
  subtotal: string
  credit: string
  tax: string
  tax2: string
  total: string
  balance: string
  status: 'Paid' | 'Unpaid' | 'Cancelled' | 'Refunded' | 'Collections' | 'Draft'
  currencycode: string
  currencyprefix: string
  currencysuffix: string
}

/** A domain from GetClientsDomains */
export interface ClientDomain {
  id: number
  userid: number
  domainname: string
  regdate: string
  nextduedate: string
  expirydate: string
  status: string
  type: string
  registrar: string
  firstpaymentamount: string
  recurringamount: string
  paymentmethod: string
  paymentmethodname: string
  subscriptionid: string
  promoid: number
  is_premium: boolean
  autorecalc: boolean
  domainstatus: string
  idnlanguage: string
  dnsmanagement: boolean
  emailforwarding: boolean
  locking: boolean
  notes: string
  addons: unknown[]
}

/** A support ticket from GetTickets */
export interface ClientTicket {
  id: number
  tid: string
  deptid: number
  deptname: string
  userid: number
  name: string
  email: string
  cc: string
  c: string
  date: string
  subject: string
  status: string
  urgency: string
  lastreply: string
  flag: number
  service: string
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
    // ── User ──────────────────────────────────────────────────────────────
    user: null as ClientUser | null,
    userLoading: false,
    userLoaded: false,

    // ── Services ──────────────────────────────────────────────────────────
    services: [] as ClientService[],
    servicesLoading: false,
    servicesLoaded: false,

    // ── Invoices ──────────────────────────────────────────────────────────
    invoices: [] as ClientInvoice[],
    invoicesLoading: false,
    invoicesLoaded: false,

    // ── Domains ───────────────────────────────────────────────────────────
    domains: [] as ClientDomain[],
    domainsLoading: false,
    domainsLoaded: false,

    // ── Tickets ───────────────────────────────────────────────────────────
    tickets: [] as ClientTicket[],
    ticketsLoading: false,
    ticketsLoaded: false
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
      console.log('[store] fetchUser: start')
      try {
        this.user = await apiFetch<ClientUser>('/api/portal/client/me')
        this.userLoaded = true
        console.log('[store] fetchUser: ok', this.user)
      } catch (err) {
        console.error('[store] fetchUser: error', err)
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
      console.log('[store] fetchServices: start')
      try {
        this.services = await apiFetch<ClientService[]>('/api/portal/client/services')
        this.servicesLoaded = true
        console.log('[store] fetchServices: ok', this.services.length)
      } catch (err) {
        console.error('[store] fetchServices: error', err)
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
      console.log('[store] fetchInvoices: start')
      try {
        this.invoices = await apiFetch<ClientInvoice[]>('/api/portal/client/invoices')
        this.invoicesLoaded = true
        console.log('[store] fetchInvoices: ok', this.invoices.length)
      } catch (err) {
        console.error('[store] fetchInvoices: error', err)
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
      console.log('[store] fetchDomains: start')
      try {
        this.domains = await apiFetch<ClientDomain[]>('/api/portal/client/domains')
        this.domainsLoaded = true
        console.log('[store] fetchDomains: ok', this.domains.length)
      } catch (err) {
        console.error('[store] fetchDomains: error', err)
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
      console.log('[store] fetchTickets: start')
      try {
        this.tickets = await apiFetch<ClientTicket[]>('/api/portal/client/tickets')
        this.ticketsLoaded = true
        console.log('[store] fetchTickets: ok', this.tickets.length)
      } catch (err) {
        console.error('[store] fetchTickets: error', err)
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
      this.user = null
      this.userLoaded = false
      this.services = []
      this.servicesLoaded = false
      this.invoices = []
      this.invoicesLoaded = false
      this.domains = []
      this.domainsLoaded = false
      this.tickets = []
      this.ticketsLoaded = false
    }
  }
})
