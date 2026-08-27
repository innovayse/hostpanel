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

/**
 * Reads the sentence to show from a failed API call.
 *
 * The wording comes from the response body and is never written here: the API is the only
 * side that knows why it refused, and a message invented in the client goes stale the moment
 * the endpoint's reasons change. The generic line is the last resort for a request that never
 * reached the API at all — an offline browser has no response body to quote.
 *
 * @param err - Whatever `apiFetch` threw.
 * @returns The message to display.
 */
function apiErrorMessage(err: unknown): string {
  const body = (err as { data?: { message?: string; statusMessage?: string } })?.data
  return body?.message
    ?? body?.statusMessage
    ?? (err as { message?: string })?.message
    ?? 'Could not reach the server.'
}

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
  /** Whether TOTP two-factor authentication is switched on for this account. */
  twoFactorEnabled?: boolean
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

/** A domain returned by the C# backend DomainDto */
export interface ClientDomain {
  /** Domain primary key. */
  id: number
  /** FK to the owning client. */
  clientId: number
  /** Full domain name (e.g. "example.com"). */
  name: string
  /** Top-level domain including the dot (e.g. ".com"). */
  tld: string
  /** Current lifecycle status (e.g. "Active", "Expired"). */
  status: string
  /** Domain registration date (ISO 8601 UTC). */
  registeredAt: string
  /** Domain expiration date (ISO 8601 UTC). */
  expiresAt: string
  /** Whether the domain is set to auto-renew at expiration. */
  autoRenew: boolean
  /** Whether WHOIS privacy is enabled. */
  whoisPrivacy: boolean
  /** Whether the domain is locked against unauthorized transfers. */
  isLocked: boolean
  /** Reference ID from the registrar's system. */
  registrarRef: string | null
  /** Authorization code for transfer. */
  eppCode: string | null
  /** FK to linked service (e.g. hosting plan). */
  linkedServiceId: number | null
  /** One-time registration cost. */
  firstPaymentAmount: number
  /** Recurring registration price. */
  recurringAmount: number
  /** Payment method label. */
  paymentMethod: string | null
  /** Applied promotion/coupon code. */
  promotionCode: string | null
  /** External payment subscription reference. */
  subscriptionId: string | null
  /** Free-text admin notes. */
  adminNotes: string | null
  /** FK to the order that created this domain. */
  orderId: number | null
  /** Order type: "Register" or "Transfer". */
  orderType: string
  /** Whether DNS management is enabled. */
  dnsManagement: boolean
  /** Whether email forwarding is enabled. */
  emailForwarding: boolean
  /** ISO 4217 currency code for the price. */
  priceCurrency: string
  /** Next renewal payment due date (ISO 8601 UTC). */
  nextDueDate: string
  /** Name of the registrar module. */
  registrar: string | null
  /** Registration period in years. */
  registrationPeriod: number
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
    // Every section carries an error beside its list. Without one, a failed fetch left the
    // list empty and the screens above read that as "you have nothing yet" — the dashboard
    // showed 0/0/0/0 and "no products/services with us yet" to an account whose four calls
    // had all answered 400. A failure and an empty account must not look the same.

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
        this.user = await apiFetch<ClientUser>('/api/portal/client/me')
        this.userLoaded = true
      } catch (err) {
        // Kept, not swallowed: the screens read this to say the section failed
        // instead of rendering it as empty.
        this.userError = apiErrorMessage(err)
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
        this.services = await apiFetch<ClientService[]>('/api/portal/client/services')
        this.servicesLoaded = true
      } catch (err) {
        // Kept, not swallowed: the screens read this to say the section failed
        // instead of rendering it as empty.
        this.servicesError = apiErrorMessage(err)
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
        this.invoices = await apiFetch<ClientInvoice[]>('/api/portal/client/invoices')
        this.invoicesLoaded = true
      } catch (err) {
        // Kept, not swallowed: the screens read this to say the section failed
        // instead of rendering it as empty.
        this.invoicesError = apiErrorMessage(err)
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
        this.domains = await apiFetch<ClientDomain[]>('/api/portal/client/domains')
        this.domainsLoaded = true
      } catch (err) {
        // Kept, not swallowed: the screens read this to say the section failed
        // instead of rendering it as empty.
        this.domainsError = apiErrorMessage(err)
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
        this.tickets = await apiFetch<ClientTicket[]>('/api/portal/client/tickets')
        this.ticketsLoaded = true
      } catch (err) {
        // Kept, not swallowed: the screens read this to say the section failed
        // instead of rendering it as empty.
        this.ticketsError = apiErrorMessage(err)
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
