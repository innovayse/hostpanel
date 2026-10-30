/**
 * Stub of the C# backend for the hostpanel client's e2e suite.
 *
 * The storefront is server-rendered: the plan list, the settings and the TLD table are fetched
 * by the Nuxt server (Nitro), not by the browser, so `page.route` can never see them. A real
 * HTTP server standing in for the backend is therefore the only mock that covers both the SSR
 * calls and the BFF calls the browser makes: the whole `browser -> Nitro BFF -> backend` path
 * stays real and only the outermost hop is replaced. This follows the testing rule: what is
 * stubbed is what the client does not own, as a real HTTP server.
 *
 * Playwright starts it as the first `webServer` entry (see `playwright.config.ts`); the Nuxt
 * server is started with `API_URL` pointing here.
 *
 * Besides the backend routes it exposes six test-only endpoints:
 * - `GET  /__e2e/requests`: every request received so far (method, path, body and the
 *   `X-Language` it carried), so a spec can assert that a form submission really reached the
 *   backend with the right body and in the right language;
 * - `POST /__e2e/script`: scripts one route to answer a given status (and problem body) until
 *   the next reset: a refused form, a session that expired, a backend that is down;
 * - `POST /__e2e/settings`: overrides one public portal setting (the template, the theme) until
 *   the next reset, so a spec can render the storefront in each template;
 * - `POST /__e2e/demo`: switches demo mode on; it is off by default, so only the spec that covers
 *   the demo banner and the sign-in hint turns it on, until the next reset;
 * - `POST /__e2e/slides`: switches the home-page slides on; the feed is empty by default (the home shows the
 *   text hero), so only the spec that covers the slider turns them on, until the next reset;
 * - `POST /__e2e/email-verified`: sets whether the signed-in address reads as confirmed (it does by default),
 *   so only the spec for the verification banner turns it off, until the next reset;
 * - `POST /__e2e/account`: overrides fields of the signed-in person's account (`GET /api/auth/me`), such as
 *   `clientPermissions` or `clientId`, so a spec can sign in a member without a permission or a first-time buyer, until the next reset;
 * - `POST /__e2e/reset`: clears the log, the scripted answers, the settings overrides, demo mode and
 *   everything a spec changed: tickets, profile, services, the domain, payment methods, contacts,
 *   the team and the two-factor flag.
 *
 * Anything it does not know answers 404 with a problem+json body, exactly as the real API
 * does for an unknown route, so a missing fixture shows up as a visible failure and not as a
 * silently empty page.
 *
 * @module e2e/support/stub-backend
 */
import { createServer } from 'node:http'
import {
  ACCESS_TOKEN,
  ACCOUNT,
  ANNOUNCEMENT,
  FAQS,
  LOCATIONS,
  PARTNERS,
  PROJECTS,
  SLIDES,
  TESTIMONIALS,
  BAD_CONTACT_EMAIL,
  BAD_CONTACT_EMAIL_MESSAGE,
  CLIENT,
  CLIENT_CREDENTIALS,
  TWO_FACTOR_CREDENTIALS,
  TWO_FACTOR_LOGIN_CODE,
  TWO_FACTOR_PENDING_TOKEN,
  DEMO_ACCOUNTS,
  DEMO_RESET_IN_MS,
  CURRENCIES,
  DEPARTMENTS,
  DOMAIN,
  DOMAIN_NAMESERVERS,
  DOMAIN_ORDER,
  EPP_CODE,
  GATEWAY_DECLINED_INVOICE,
  GATEWAY_METHODS,
  GATEWAY_PAID_INVOICE,
  GATEWAY_PENDING_INVOICE,
  INVOICE,
  KB_ARTICLES,
  ORDER,
  PRODUCT_GROUPS,
  HOSTING_SPECIFICATIONS,
  productsIn,
  PUBLIC_SETTINGS,
  SAVED_CARD,
  SEEDED_CONTACT,
  SEEDED_TICKET,
  SEEDED_TICKET_THREAD,
  SERVICE,
  SERVICE_INVOICE,
  SERVICE_MANAGED,
  SSH_ACCESS,
  SERVICE_PENDING,
  SERVICE_SUSPENDED,
  STUB_PORT,
  TAKEN_DOMAIN,
  TAKEN_EMAIL,
  TAKEN_EMAIL_MESSAGE,
  TEAM_MEMBER,
  TEAM_OWNER,
  TLD_PRICING,
  TLD_PRICING_AMD,
  REGISTRANT_CONTACT,
  WHOIS_RECORD,
  TWO_FACTOR,
  TWO_FACTOR_REFUSED,
} from './fixtures.mjs'

/**
 * One request the stub has served.
 *
 * @typedef {object} LoggedRequest
 * @property {string} method HTTP method.
 * @property {string} path Path without the query string.
 * @property {unknown} body Parsed JSON body, or `null` when there was none.
 * @property {string} language The `X-Language` header the request carried, or an empty string.
 */

/**
 * An answer a spec scripted for one route.
 *
 * @typedef {object} ScriptedAnswer
 * @property {string} method HTTP method of the scripted route.
 * @property {string} path Path of the scripted route.
 * @property {number} status HTTP status to answer.
 * @property {string} [code] Error code carried in the problem body's `type`.
 * @property {string} [detail] The sentence carried in the problem body's `detail`.
 * @property {Record<string, string[]>} [errors] Field messages of a 400 `VALIDATION_ERROR`.
 */

/**
 * One ticket the stub holds.
 *
 * @typedef {object} StubTicket
 * @property {Record<string, unknown>} row The ticket as the list endpoint returns it.
 * @property {string} message The opening message.
 * @property {Record<string, unknown>[]} replies The conversation after the opening message.
 */

/** @type {LoggedRequest[]} Every request received since the last reset. */
const received = []

/** @type {Map<string, ScriptedAnswer>} Scripted answers, keyed `METHOD /path`, cleared on reset. */
const scripted = new Map()

/**
 * Builds the ticket the stub starts with, so a reset can restore it.
 *
 * @returns {StubTicket[]} A fresh list holding the seeded ticket.
 */
const seedTickets = () => [
  {
    row: { ...SEEDED_TICKET },
    message: SEEDED_TICKET_THREAD.message,
    replies: SEEDED_TICKET_THREAD.replies.map((reply) => ({ ...reply })),
  },
]

/** @type {StubTicket[]} The tickets the client area lists; specs create and reply to them. */
let tickets = seedTickets()

/** @type {Map<string, string>} Public settings a spec overrode, keyed by setting key; cleared on reset. */
const settingOverrides = new Map()

/** The account `GET /api/auth/me` answers; a spec overrides fields of it, a reset restores it. */
let account = { ...ACCOUNT }

/** Whether the stub reports a demo; off by default, a spec turns it on, a reset turns it off. */
let demoEnabled = false

/** Whether the stub serves home-page slides; off by default, a spec turns it on, a reset turns it off. */
let slidesEnabled = false

/** Whether the stub reports the signed-in address as confirmed; on by default, a spec turns it off, a reset turns it on. */
let emailVerified = true

/** @type {Record<string, unknown>} The signed-in customer's profile; a saved edit changes it, a reset restores it. */
let profile = { ...CLIENT }

/**
 * Builds the services the stub starts with, so a reset can restore them.
 *
 * @returns {Record<string, unknown>[]} A fresh list: the set-up service with a domain, the set-up one without, the one that needs setup and the suspended one.
 */
const seedServices = () => [{ ...SERVICE }, { ...SERVICE_MANAGED }, { ...SERVICE_PENDING }, { ...SERVICE_SUSPENDED }]

/** @type {Record<string, unknown>[]} The customer's services; a finished setup gives one a control-panel user, a reset restores them. */
let services = seedServices()

/** @type {Record<string, unknown>} The registered domain; the switches and edits change it, a reset restores it. */
let domainState = { ...DOMAIN }

/**
 * Builds the nameservers the domain starts with, so a reset can restore them.
 *
 * @returns {{ id: number, host: string }[]} A fresh list.
 */
const seedNameservers = () => DOMAIN_NAMESERVERS.map((nameserver) => ({ ...nameserver }))

/** @type {{ id: number, host: string }[]} The domain's nameservers. */
let nameservers = seedNameservers()

/**
 * The domain as the backend answers it: the stored state with its current nameservers inside
 * `management`, where `MyDomainDto` carries them.
 *
 * @returns {Record<string, unknown>} The domain.
 */
const domainView = () => ({ ...domainState, management: { ...DOMAIN.management, nameservers } })

/** @type {Record<string, unknown>[]} The saved payment methods; a removal changes them, a reset restores them. */
let savedCards = [{ ...SAVED_CARD }]

/** @type {Record<string, unknown>[]} The account's contacts; an add, an edit and a removal change them. */
let contacts = [{ ...SEEDED_CONTACT }]

/** @type {Record<string, unknown>[]} The account's team; a removal changes it. */
let team = [{ ...TEAM_OWNER }, { ...TEAM_MEMBER }]

/** @type {Record<string, unknown>[]} SSH keys of the managed service; an add and a revoke change them, a reset empties them. */
let sshKeys = []

/** Running number that makes each stub SSH key id unique. */
let lastSshKey = 0

/** @type {number} Last contact id handed out, so a created contact gets a fresh one. */
let lastContactId = Number(SEEDED_CONTACT.id)

/**
 * The public settings as the backend serves them: the fixture list with the spec's overrides applied.
 *
 * @returns {{ key: string, value: string }[]} The settings, overridden values replacing the fixture ones.
 */
const publicSettings = () => [
  ...PUBLIC_SETTINGS.filter((setting) => !settingOverrides.has(setting.key)),
  ...[...settingOverrides.entries()].map(([key, value]) => ({ key, value })),
]

/**
 * Reads and parses a JSON request body.
 *
 * @param {import('node:http').IncomingMessage} req The incoming request.
 * @returns {Promise<unknown>} The parsed body, or `null` when it is empty or not JSON.
 */
const readJson = async (req) => {
  const chunks = []
  for await (const chunk of req) {
    chunks.push(chunk)
  }
  const text = Buffer.concat(chunks).toString('utf8')
  if (!text) {
    return null
  }
  try {
    return JSON.parse(text)
  } catch {
    return null
  }
}

/**
 * Writes a JSON response.
 *
 * @param {import('node:http').ServerResponse} res The response to write.
 * @param {number} status HTTP status code.
 * @param {unknown} payload Value serialised as the body; `undefined` sends no body.
 * @param {string} [type] Content type, `application/json` unless an error body asks for problem+json.
 */
const send = (res, status, payload, type = 'application/json') => {
  if (payload === undefined) {
    res.writeHead(status)
    res.end()
    return
  }
  res.writeHead(status, { 'Content-Type': type })
  res.end(JSON.stringify(payload))
}

/**
 * Builds one page of a list in the backend's `PaginatedList` shape: every row on the first page.
 *
 * @template T
 * @param {T[]} items The rows.
 * @param {number} pageSize The page size the caller asked for.
 * @returns {{ items: T[], pageNumber: number, pageSize: number, totalCount: number, totalPages: number }} The page.
 */
const firstPage = (items, pageSize) => ({
  items,
  pageNumber: 1,
  pageSize,
  totalCount: items.length,
  totalPages: items.length === 0 ? 0 : 1,
})

/**
 * Writes the backend's `ErrorResponse` shape (problem+json) the BFF knows how to forward.
 *
 * @param {import('node:http').ServerResponse} res The response to write.
 * @param {number} status HTTP status code.
 * @param {string} code Machine-readable error code, carried in `type`.
 * @param {string} detail The human sentence, carried in `detail`.
 * @param {Record<string, string[]>} [errors] Field messages, carried in `errors`.
 */
const sendProblem = (res, status, code, detail, errors) => {
  send(res, status, { type: code, title: detail, status, detail, ...(errors ? { errors } : {}) }, 'application/problem+json')
}

/**
 * Answers one backend route, or `false` when the stub has no fixture for it.
 *
 * @param {string} method HTTP method.
 * @param {string} path Request path without the query string.
 * @param {unknown} body Parsed JSON body, or `null`.
 * @param {import('node:http').ServerResponse} res The response to write.
 * @param {URLSearchParams} [query] The request's query string, read by the routes that filter, page or price by `currency`.
 * @returns {boolean} True when the route was handled.
 */
const route = (method, path, body, res, query = new URLSearchParams()) => {
  const isGet = method === 'GET'

  // Public storefront data: read by the Nuxt server while rendering.
  if (isGet && path === '/api/settings/public') {
    send(res, 200, publicSettings())
    return true
  }
  if (isGet && path === '/api/currencies') {
    send(res, 200, CURRENCIES)
    return true
  }
  if (isGet && path === '/api/public/product-groups') {
    send(res, 200, PRODUCT_GROUPS)
    return true
  }
  if (isGet && path === '/api/public/products') {
    // Like the real endpoint: `productId` selects one product, `groupId` one group, repeated `groupIds` several groups,
    // none of them the whole catalogue; a product must satisfy every filter given.
    const groupIds = query.getAll('groupIds').map(Number)
    const groupId = query.get('groupId')
    const productId = query.get('productId')
    const wanted = productsIn(query.get('currency'))
      .filter((product) => groupIds.length === 0 || groupIds.includes(product.groupId))
      .filter((product) => groupId === null || String(product.groupId) === groupId)
      .filter((product) => productId === null || String(product.id) === productId)
    send(res, 200, wanted)
    return true
  }
  if (isGet && path === '/api/product-features') {
    send(res, 200, query.get('groupId') === '1' || query.get('gid') === '1' ? HOSTING_SPECIFICATIONS : [])
    return true
  }
  if (isGet && path === '/api/domains/tld-pricing') {
    send(res, 200, query.get('currency') === 'AMD' ? TLD_PRICING_AMD : TLD_PRICING)
    return true
  }
  if (method === 'POST' && path === '/api/domains/check') {
    const domain = String(body?.domain ?? '')
    send(res, 200, {
      domain,
      available: domain !== TAKEN_DOMAIN,
      status: domain === TAKEN_DOMAIN ? 'unavailable' : 'available',
    })
    return true
  }
  if (isGet && path === '/api/announcements/published') {
    send(res, 200, firstPage([ANNOUNCEMENT], Number(query.get('pageSize') ?? 10)))
    return true
  }
  if (isGet && path.startsWith('/api/public/announcements/')) {
    if (path === `/api/public/announcements/${ANNOUNCEMENT.id}`) {
      send(res, 200, ANNOUNCEMENT)
    } else {
      sendProblem(res, 404, 'NOT_FOUND', 'Announcement not found.')
    }
    return true
  }
  if (isGet && path === '/api/knowledgebase/categories') {
    const counts = new Map()
    for (const article of KB_ARTICLES) {
      counts.set(article.category, (counts.get(article.category) ?? 0) + 1)
    }
    send(res, 200, [...counts.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([name, articleCount]) => ({ name, articleCount })))
    return true
  }
  if (isGet && path === '/api/knowledgebase/articles') {
    const category = (query.get('category') ?? '').toLowerCase()
    const search = (query.get('search') ?? '').toLowerCase()
    const items = KB_ARTICLES
      .filter((a) => !category || a.category.toLowerCase() === category)
      .filter((a) => !search || a.title.toLowerCase().includes(search) || a.content.toLowerCase().includes(search))
      .map((a) => ({ id: a.id, title: a.title, excerpt: a.content.replace(/<[^>]*>/g, ' ').trim(), category: a.category }))
    send(res, 200, firstPage(items, Number(query.get('pageSize') ?? 100)))
    return true
  }
  if (isGet && path.startsWith('/api/knowledgebase/')) {
    const article = KB_ARTICLES.find((a) => `/api/knowledgebase/${a.id}` === path)
    if (article) {
      send(res, 200, article)
    } else {
      sendProblem(res, 404, 'NOT_FOUND', 'Article not found.')
    }
    return true
  }
  if (isGet && path === '/api/departments') {
    send(res, 200, DEPARTMENTS)
    return true
  }
  if (isGet && path === '/api/payment-methods') {
    send(res, 200, GATEWAY_METHODS)
    return true
  }

  // Contact form: the backend answers 204 and the BFF relays it; one address is refused
  // with a field error so the form's refusal path can be driven.
  if (method === 'POST' && path === '/api/contact') {
    if (body?.email === BAD_CONTACT_EMAIL) {
      sendProblem(res, 400, 'VALIDATION_ERROR', 'Validation failed.', { email: [BAD_CONTACT_EMAIL_MESSAGE] })
    } else {
      send(res, 204)
    }
    return true
  }

  // Registration and password recovery (local auth mode); one address is refused with a field error.
  if (method === 'POST' && path === '/api/auth/register') {
    if (body?.email === TAKEN_EMAIL) {
      sendProblem(res, 400, 'VALIDATION_ERROR', 'Validation failed.', { email: [TAKEN_EMAIL_MESSAGE] })
    } else {
      send(res, 200, { userId: 'u-e2e' })
    }
    return true
  }
  if (method === 'POST' && path === '/api/auth/forgot-password') {
    send(res, 200, 'ok')
    return true
  }

  // Email verification: status read and the authenticated resend (204, no body).
  if (isGet && path === '/api/auth/email-verified') {
    send(res, 200, { verified: emailVerified })
    return true
  }
  if (method === 'POST' && path === '/api/auth/confirm-email') {
    send(res, 200, 'ok')
    return true
  }
  if (method === 'POST' && path === '/api/auth/resend-verification') {
    send(res, 204)
    return true
  }

  // Storefront content the operator manages: anonymous, read by the Nuxt server while rendering.
  if (isGet && path === '/api/public/faqs') {
    send(res, 200, FAQS)
    return true
  }
  if (isGet && path === '/api/public/testimonials') {
    send(res, 200, TESTIMONIALS)
    return true
  }
  if (isGet && path === '/api/public/partners') {
    send(res, 200, PARTNERS)
    return true
  }
  if (isGet && path === '/api/public/locations') {
    send(res, 200, LOCATIONS)
    return true
  }
  if (isGet && path === '/api/public/projects') {
    send(res, 200, PROJECTS)
    return true
  }

  // Demo mode: anonymous, off unless a spec switched it on; the next reset is always DEMO_RESET_IN_MS from now.
  if (isGet && path === '/api/slides') {
    send(res, 200, slidesEnabled ? SLIDES : [])
    return true
  }
  if (isGet && path === '/api/public/demo') {
    send(res, 200, { enabled: demoEnabled, nextResetAt: new Date(Date.now() + DEMO_RESET_IN_MS).toISOString(), accounts: DEMO_ACCOUNTS })
    return true
  }

  // Sign-in (local auth mode).
  if (method === 'POST' && path === '/api/auth/login') {
    const ok = body?.email === CLIENT_CREDENTIALS.email && body?.password === CLIENT_CREDENTIALS.password
    const needsCode = body?.email === TWO_FACTOR_CREDENTIALS.email && body?.password === TWO_FACTOR_CREDENTIALS.password
    if (needsCode) {
      // A two-factor account gets the challenge, not a session.
      send(res, 200, { twoFactorRequired: true, pendingToken: TWO_FACTOR_PENDING_TOKEN })
    } else if (ok) {
      send(res, 200, { accessToken: ACCESS_TOKEN, expiresIn: 900 })
    } else {
      sendProblem(res, 401, 'INVALID_CREDENTIALS', 'Invalid email or password.')
    }
    return true
  }

  // Second sign-in step: the challenge token plus the one accepted code finish the sign-in.
  if (method === 'POST' && path === '/api/auth/2fa-login') {
    if (body?.pendingToken === TWO_FACTOR_PENDING_TOKEN && body?.code === TWO_FACTOR_LOGIN_CODE) {
      send(res, 200, { accessToken: ACCESS_TOKEN, expiresIn: 900 })
    } else {
      sendProblem(res, 401, 'TWO_FACTOR_CODE_UNAUTHORIZED', TWO_FACTOR_REFUSED)
    }
    return true
  }

  // Client area: the stub trusts any bearer; the specs do not test authorisation here.
  if (isGet && path === '/api/auth/me') {
    send(res, 200, account)
    return true
  }
  if (isGet && path === '/api/clients/me') {
    send(res, 200, profile)
    return true
  }
  if (isGet && path === '/api/me/services') {
    send(res, 200, services)
    return true
  }
  if (isGet && path === '/api/me/domains') {
    send(res, 200, [domainView()])
    return true
  }
  if (method === 'PATCH' && path === '/api/clients/me') {
    profile = { ...profile, firstName: body?.firstName ?? profile.firstName }
    send(res, 200, profile)
    return true
  }

  // Support tickets: kept in memory so a created ticket and a reply show up on the next read.
  if (isGet && path === '/api/me/tickets') {
    send(res, 200, tickets.map((ticket) => ticket.row))
    return true
  }
  if (method === 'POST' && path === '/api/me/tickets') {
    const id = 900 + tickets.length
    const department = DEPARTMENTS.find((d) => String(d.id) === String(body?.departmentId))
    tickets.push({
      row: {
        ...SEEDED_TICKET,
        id,
        departmentName: department?.name ?? null,
        createdAt: '2030-02-02T10:00:00Z',
        subject: String(body?.subject ?? ''),
        priority: String(body?.priority ?? 'Medium'),
        replyCount: 0,
        lastReplyAt: null,
      },
      message: String(body?.message ?? ''),
      replies: [],
    })
    send(res, 200, { id })
    return true
  }
  const ticketMatch = path.match(/^\/api\/me\/tickets\/(\d+)(\/reply)?$/)
  if (ticketMatch) {
    const ticket = tickets.find((t) => String(t.row.id) === ticketMatch[1])
    if (!ticket) {
      sendProblem(res, 404, 'NOT_FOUND', 'Ticket not found.')
      return true
    }
    if (method === 'POST' && ticketMatch[2]) {
      ticket.replies.push({
        id: ticket.replies.length + 100,
        message: String(body?.message ?? ''),
        // The backend names the author from the signed-in caller, never from the body.
        authorName: 'Test Client',
        isStaffReply: false,
        createdAt: '2030-02-02T12:00:00Z',
      })
      send(res, 204)
      return true
    }
    if (isGet && !ticketMatch[2]) {
      send(res, 200, {
        id: ticket.row.id,
        subject: ticket.row.subject,
        message: ticket.message,
        status: ticket.row.status,
        priority: ticket.row.priority,
        departmentId: 1,
        departmentName: ticket.row.departmentName,
        createdAt: ticket.row.createdAt,
        replies: ticket.replies,
        rating: null,
        feedbackComment: null,
        feedbackAt: null,
      })
      return true
    }
  }
  if (isGet && path === '/api/me/invoices') {
    send(res, 200, [INVOICE])
    return true
  }
  if (isGet && path === `/api/me/invoices/${INVOICE.id}`) {
    send(res, 200, INVOICE)
    return true
  }
  if (isGet && path === '/api/me/payment-methods') {
    send(res, 200, savedCards)
    return true
  }
  // Hosted-gateway return: the invoice id picks the outcome (an unknown id is a 404, so the page cannot tell); the state is spelled as the API sends its enum.
  const gatewayMatch = path.match(/^\/api\/me\/invoices\/(\d+)\/gateway-payment\/complete$/)
  if (method === 'POST' && gatewayMatch) {
    const outcome = { [GATEWAY_PAID_INVOICE]: 'Paid', [GATEWAY_PENDING_INVOICE]: 'Pending', [GATEWAY_DECLINED_INVOICE]: 'Declined' }[gatewayMatch[1]]
    if (outcome) {
      send(res, 200, { state: outcome })
    } else {
      sendProblem(res, 404, 'NOT_FOUND', 'Invoice not found.')
    }
    return true
  }
  if (method === 'POST' && path === '/api/auth/accept-invite') {
    send(res, 200, { success: true })
    return true
  }
  if (method === 'POST' && path === `/api/me/invoices/${INVOICE.id}/pay`) {
    send(res, 200, { success: true })
    return true
  }

  // Order placement: the order, its invoice and the token that proves the order is the caller's.
  if (method === 'POST' && path === '/api/orders') {
    send(res, 200, ORDER)
    return true
  }

  return routeManagement(method, path, body, res)
}

/**
 * Answers the routes of the client area's management screens: one service, one domain, the
 * account's payment methods, contacts, team and two-factor.
 *
 * @param {string} method HTTP method.
 * @param {string} path Request path without the query string.
 * @param {unknown} body Parsed JSON body, or `null`.
 * @param {import('node:http').ServerResponse} res The response to write.
 * @returns {boolean} True when the route was handled.
 */
const routeManagement = (method, path, body, res) => {
  const isGet = method === 'GET'

  // SSH keys of one service: the access summary, the list, an add and a revoke. Only the managed
  // service offers them; the real backend resolves server, port and account from the service.
  const sshMatch = path.match(/^\/api\/me\/services\/(\d+)\/ssh(?:\/keys(?:\/([^/]+))?)?$/)
  if (sshMatch) {
    const service = services.find((s) => String(s.id) === sshMatch[1])
    if (!service) {
      sendProblem(res, 404, 'SERVICE_NOT_FOUND', 'No such client service.')
      return true
    }
    if (isGet && path.endsWith('/ssh')) {
      send(res, 200, { ...SSH_ACCESS, username: service.username, connectCommand: `ssh -p ${SSH_ACCESS.port} ${service.username}@${SSH_ACCESS.hostname}` })
      return true
    }
    if (isGet && path.endsWith('/ssh/keys')) {
      send(res, 200, sshKeys.map((key) => Object.fromEntries(Object.entries(key).filter(([name]) => name !== 'blob'))))
      return true
    }
    if (method === 'POST' && path.endsWith('/ssh/keys')) {
      const blob = String(body?.publicKey ?? '').split(' ')[1] ?? ''
      if (!String(body?.publicKey ?? '').startsWith('ssh-ed25519 ')) {
        sendProblem(res, 400, 'SSH_KEY_INVALID', 'The key is not a valid ED25519 public key.')
        return true
      }
      if (sshKeys.some((k) => k.blob === blob)) {
        sendProblem(res, 409, 'SSH_KEY_ALREADY_INSTALLED', 'This key is already installed on the service.')
        return true
      }
      lastSshKey += 1
      const key = {
        keyId: `00000000-0000-4000-8000-${String(lastSshKey).padStart(12, '0')}`,
        label: String(body?.label ?? ''),
        fingerprint: `SHA256:e2e-fingerprint-${lastSshKey}`,
        createdAt: '2026-10-27T00:00:00Z',
        syncState: 'Installed',
        isStale: false,
        presentOnServer: true,
      }
      sshKeys = [...sshKeys, { ...key, blob }]
      send(res, 201, key)
      return true
    }
    if (method === 'DELETE' && sshMatch[2]) {
      if (!sshKeys.some((k) => k.keyId === sshMatch[2])) {
        sendProblem(res, 404, 'SSH_KEY_NOT_FOUND', 'No such SSH key.')
        return true
      }
      sshKeys = sshKeys.filter((k) => k.keyId !== sshMatch[2])
      send(res, 204)
      return true
    }
  }

  // One service: cancellation state, related invoices, password, cancellation and first-time setup.
  const serviceMatch = path.match(/^\/api\/me\/services\/(\d+)\/(cancellation-status|hosting-info|invoices|change-password|cancel|setup)$/)
  if (serviceMatch) {
    const service = services.find((s) => String(s.id) === serviceMatch[1])
    if (!service) {
      sendProblem(res, 404, 'NOT_FOUND', 'Service not found.')
      return true
    }
    if (isGet && serviceMatch[2] === 'cancellation-status') {
      send(res, 200, { pending: false })
      return true
    }
    if (isGet && serviceMatch[2] === 'hosting-info') {
      // The real backend probes the domain over the network; a spec gets the empty reading.
      send(res, 200, { nameservers: [], ssl: null })
      return true
    }
    if (isGet && serviceMatch[2] === 'invoices') {
      send(res, 200, { invoices: [SERVICE_INVOICE], unattributedInvoiceCount: 0 })
      return true
    }
    if (method === 'POST' && serviceMatch[2] === 'setup') {
      // The typed domain is deliberately not stored: a service with a domain makes the
      // backend probe it over the network, which a spec must not depend on.
      service.username = String(body?.username ?? '')
      service.status = 'Active'
      send(res, 204)
      return true
    }
    if (method === 'POST') {
      send(res, 204)
      return true
    }
  }

  // One domain: detail, the three switches, nameservers, the authorisation code and the renewal order.
  if (method === 'POST' && path === '/api/me/domains/transfer-order') {
    send(res, 200, DOMAIN_ORDER)
    return true
  }
  const domainMatch = path.match(/^\/api\/me\/domains\/(\d+)(?:\/(auto-renew|whois-privacy|whois|contact|lock|nameservers|epp|renew))?$/)
  if (domainMatch) {
    if (String(domainState.id) !== domainMatch[1]) {
      sendProblem(res, 404, 'NOT_FOUND', 'Domain not found.')
      return true
    }
    const part = domainMatch[2]
    const switches = { 'auto-renew': 'autoRenew', 'whois-privacy': 'whoisPrivacy', lock: 'isLocked' }
    if (isGet && !part) {
      send(res, 200, domainView())
      return true
    }
    if (method === 'PUT' && part && switches[part]) {
      domainState = { ...domainState, [switches[part]]: Boolean(body?.enabled) }
      send(res, 204)
      return true
    }
    if (isGet && part === 'whois') {
      send(res, 200, WHOIS_RECORD)
      return true
    }
    if (isGet && part === 'contact') {
      send(res, 200, REGISTRANT_CONTACT)
      return true
    }
    if (method === 'PUT' && part === 'whois') {
      send(res, 204)
      return true
    }
    if (method === 'PUT' && part === 'nameservers') {
      nameservers = (body?.nameservers ?? []).map((host, index) => ({ id: index + 1, host: String(host) }))
      send(res, 204)
      return true
    }
    if (method === 'POST' && part === 'epp') {
      send(res, 200, { eppCode: EPP_CODE })
      return true
    }
    if (method === 'POST' && part === 'renew') {
      send(res, 200, DOMAIN_ORDER)
      return true
    }
  }

  // Saved payment methods: set the default, remove.
  const cardMatch = path.match(/^\/api\/me\/payment-methods\/([^/]+)$/)
  if (cardMatch && (method === 'PUT' || method === 'DELETE')) {
    if (!savedCards.some((card) => card.id === cardMatch[1])) {
      sendProblem(res, 404, 'NOT_FOUND', 'Payment method not found.')
      return true
    }
    if (method === 'DELETE') {
      savedCards = savedCards.filter((card) => card.id !== cardMatch[1])
    }
    send(res, 204)
    return true
  }

  // Contacts of the account, kept in memory so an add, an edit and a removal show on the next read.
  if (isGet && path === '/api/clients/me/contacts') {
    send(res, 200, contacts)
    return true
  }
  if (method === 'POST' && path === '/api/clients/me/contacts') {
    lastContactId += 1
    contacts.push({ ...SEEDED_CONTACT, ...body, id: lastContactId })
    send(res, 204)
    return true
  }
  const contactMatch = path.match(/^\/api\/clients\/me\/contacts\/(\d+)$/)
  if (contactMatch && (method === 'PUT' || method === 'DELETE')) {
    const contact = contacts.find((c) => String(c.id) === contactMatch[1])
    if (!contact) {
      sendProblem(res, 404, 'NOT_FOUND', 'Contact not found.')
      return true
    }
    if (method === 'PUT') {
      Object.assign(contact, body)
    } else {
      contacts = contacts.filter((c) => String(c.id) !== contactMatch[1])
    }
    send(res, 204)
    return true
  }

  // The team: list, invite (accepted, not listed until the invitee accepts), remove.
  if (isGet && path === '/api/me/users') {
    send(res, 200, team)
    return true
  }
  if (method === 'POST' && path === '/api/me/users/invite') {
    send(res, 204)
    return true
  }
  const userMatch = path.match(/^\/api\/me\/users\/([^/]+)$/)
  if (method === 'DELETE' && userMatch) {
    team = team.filter((member) => member.userId !== userMatch[1])
    send(res, 204)
    return true
  }

  // Mail history of the account: empty, one page of the hundred rows the BFF always asks for.
  if (isGet && path === '/api/me/emails') {
    send(res, 200, firstPage([], 100))
    return true
  }

  // Two-factor: enrolment, then the flag on the profile follows the accepted code.
  if (method === 'POST' && path === '/api/me/2fa/setup') {
    send(res, 200, { secret: TWO_FACTOR.secret, qrCodeUri: TWO_FACTOR.qrCodeUri })
    return true
  }
  if (method === 'POST' && (path === '/api/me/2fa/enable' || path === '/api/me/2fa/disable')) {
    profile = { ...profile, twoFactorEnabled: path.endsWith('/enable') }
    send(res, 204)
    return true
  }

  return false
}

/**
 * Answers a scripted route, when a spec scripted one.
 *
 * @param {string} method HTTP method.
 * @param {string} path Request path without the query string.
 * @param {import('node:http').ServerResponse} res The response to write.
 * @returns {boolean} True when a scripted answer was written.
 */
const answerScripted = (method, path, res) => {
  const answer = scripted.get(`${method} ${path}`)
  if (!answer) {
    return false
  }
  sendProblem(res, answer.status, answer.code ?? 'SCRIPTED', answer.detail ?? 'Scripted failure.', answer.errors)
  return true
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url ?? '/', 'http://stub.local')
  const method = req.method ?? 'GET'
  const path = url.pathname

  // Test-only control endpoints; never logged themselves.
  if (path === '/__e2e/requests') {
    send(res, 200, received)
    return
  }
  if (path === '/__e2e/reset') {
    received.length = 0
    scripted.clear()
    settingOverrides.clear()
    demoEnabled = false
    slidesEnabled = false
    account = { ...ACCOUNT }
    emailVerified = true
    tickets = seedTickets()
    profile = { ...CLIENT }
    services = seedServices()
    domainState = { ...DOMAIN }
    nameservers = seedNameservers()
    savedCards = [{ ...SAVED_CARD }]
    contacts = [{ ...SEEDED_CONTACT }]
    team = [{ ...TEAM_OWNER }, { ...TEAM_MEMBER }]
    sshKeys = []
    lastSshKey = 0
    lastContactId = Number(SEEDED_CONTACT.id)
    send(res, 204)
    return
  }
  if (path === '/__e2e/email-verified') {
    const state = await readJson(req)
    emailVerified = Boolean(state.verified)
    send(res, 204)
    return
  }
  if (path === '/__e2e/settings') {
    const setting = await readJson(req)
    settingOverrides.set(String(setting.key), String(setting.value))
    send(res, 204)
    return
  }
  if (path === '/__e2e/account') {
    account = { ...ACCOUNT, ...(await readJson(req)) }
    send(res, 204)
    return
  }
  if (path === '/__e2e/demo') {
    const demo = await readJson(req)
    demoEnabled = demo.enabled === true
    send(res, 204)
    return
  }
  if (path === '/__e2e/slides') {
    const slides = await readJson(req)
    slidesEnabled = slides.enabled === true
    send(res, 204)
    return
  }
  if (path === '/__e2e/script') {
    const answer = await readJson(req)
    scripted.set(`${answer.method} ${answer.path}`, answer)
    send(res, 204)
    return
  }

  const body = method === 'GET' ? null : await readJson(req)
  received.push({ method, path, query: url.search, body, language: String(req.headers['x-language'] ?? '') })

  if (answerScripted(method, path, res)) {
    return
  }
  if (!route(method, path, body, res, url.searchParams)) {
    sendProblem(res, 404, 'NOT_FOUND', `No e2e fixture for ${method} ${path}.`)
  }
})

server.listen(STUB_PORT, '127.0.0.1', () => {
  process.stdout.write(`e2e stub backend listening on ${STUB_PORT}\n`)
})
