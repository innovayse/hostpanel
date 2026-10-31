/**
 * Fixtures shared by the e2e stub backend (`stub-backend.mjs`) and the specs.
 *
 * One module on purpose: a spec asserts on the very strings the stub serves, so the two can
 * never drift apart. Plain `.mjs` because the stub is started by Playwright's `webServer` with
 * `node` directly, with no TypeScript loader in between.
 *
 * Shapes follow what the C# backend sends (camelCase `ProductDto`, `InvoiceDto` and the rest); the Nuxt
 * BFF in `server/api/portal/**` maps them exactly as it does in production.
 *
 * @module e2e/support/fixtures
 */

/** Port the stub backend listens on; the Nuxt server is pointed at it through `API_URL`. */
export const STUB_PORT = Number(process.env.E2E_STUB_PORT || 5999)

/** Base URL of the stub backend, used by specs to read what it received. */
export const STUB_URL = `http://127.0.0.1:${STUB_PORT}`

/** Access token the stub hands out on sign-in; the stub accepts any bearer, so this only needs to be non-empty. */
export const ACCESS_TOKEN = 'e2e-access-token'

/** Password the stub refuses, to exercise the sign-in failure path. */
export const WRONG_PASSWORD = 'wrong-password'

/** The hosting plan the plan-to-cart flow buys. */
export const PLAN = {
  id: 11,
  groupId: 1,
  name: 'E2E Starter',
  description: 'Starter hosting for the end-to-end suite',
  status: 'Active',
  pricing: { monthly: 5, annual: 48 },
  prices: [
    { currencyCode: 'USD', monthly: 5, annual: 48 },
    { currencyCode: 'AMD', monthly: 2000, annual: 19200 },
  ],
}

/** A second, dearer plan so the pricing page has more than one card to choose between. */
export const PLAN_PRO = {
  id: 12,
  groupId: 1,
  name: 'E2E Business',
  description: 'Business hosting for the end-to-end suite',
  status: 'Active',
  pricing: { monthly: 15, annual: 144 },
  // The operator's recommended plan, so the pricing pages draw their badge.
  isFeatured: true,
  prices: [
    { currencyCode: 'USD', monthly: 15, annual: 144 },
    { currencyCode: 'AMD', monthly: 6000, annual: 57600 },
  ],
}

/**
 * The product a domain order hangs on. The storefront finds it by name (a product whose name
 * contains "domain") and adds a domain to the cart only when it exists; it sits in a group of its
 * own so no hosting page lists it.
 */
export const DOMAIN_PRODUCT = {
  id: 90,
  groupId: 9,
  name: 'E2E Domain Registration',
  description: 'The product domain orders are placed on',
  status: 'Active',
  pricing: { monthly: 0, annual: 0 },
  prices: [
    { currencyCode: 'USD', monthly: 0, annual: 0 },
    { currencyCode: 'AMD', monthly: 0, annual: 0 },
  ],
}

/**
 * Builds one catalogue plan of an extra family group, so the home page has more families than it
 * shows.
 *
 * @param {number} id Product id.
 * @param {number} groupId Group the plan belongs to.
 * @param {string} name Plan name.
 * @param {number} monthly Monthly price in USD.
 * @param {string} type Product type the catalogue reports.
 * @returns {object} The plan, priced in USD and AMD.
 */
const familyPlan = (id, groupId, name, monthly, type) => ({
  id,
  groupId,
  name,
  description: `${name} plan for the end-to-end suite`,
  status: 'Active',
  type,
  pricing: { monthly, annual: monthly * 10 },
  prices: [
    { currencyCode: 'USD', monthly, annual: monthly * 10 },
    { currencyCode: 'AMD', monthly: monthly * 400, annual: monthly * 4000 },
  ],
})

/** Plans of the extra families: five VPS plans (an odd count, to test the centred last row), then one each for dedicated servers, email and SSL. */
export const EXTRA_FAMILY_PLANS = [
  familyPlan(21, 2, 'E2E VPS Small', 9, 'Vps'),
  familyPlan(22, 2, 'E2E VPS Medium', 18, 'Vps'),
  familyPlan(23, 2, 'E2E VPS Large', 36, 'Vps'),
  familyPlan(24, 2, 'E2E VPS Pro', 72, 'Vps'),
  familyPlan(25, 2, 'E2E VPS Max', 144, 'Vps'),
  familyPlan(31, 3, 'E2E Dedicated One', 80, 'Dedicated'),
  familyPlan(41, 4, 'E2E Mailbox', 3, 'Email'),
  familyPlan(51, 5, 'E2E SSL Basic', 12, 'Ssl'),
]

/** Every product the catalogue endpoint serves. */
export const PRODUCTS = [PLAN, PLAN_PRO, DOMAIN_PRODUCT, ...EXTRA_FAMILY_PLANS]

/** The hosting product group, the family the product page is headed by. */
export const HOSTING_GROUP = { id: 1, name: 'Web Hosting', description: null, productCount: 2 }

/**
 * The catalogue's product groups as `GET /api/public/product-groups` serves them: active groups only,
 * with no staff field. The hosting group is a product family; the domain product's group (9) is
 * switched off, so the endpoint leaves it out and no family page lists the domain product.
 */
export const PRODUCT_GROUPS = [
  HOSTING_GROUP,
  { id: 2, name: 'VPS Hosting', description: 'Virtual servers with root access for the end-to-end suite', productCount: 5 },
  { id: 3, name: 'Dedicated Servers', description: null, productCount: 1 },
  { id: 4, name: 'Business Email', description: null, productCount: 1 },
  { id: 5, name: 'SSL Certificates', description: null, productCount: 1 },
]

/**
 * The specification lines of the hosting group, as `GET /api/product-features` serves them: the
 * same labels for both plans, so the plan comparison has rows to show.
 */
export const HOSTING_SPECIFICATIONS = [
  { id: 1, productId: PLAN.id, label: 'Disk space', value: '10 GB', sortOrder: 1 },
  { id: 2, productId: PLAN_PRO.id, label: 'Disk space', value: '50 GB', sortOrder: 1 },
  { id: 3, productId: PLAN.id, label: 'Websites', value: '1', sortOrder: 2 },
  { id: 4, productId: PLAN_PRO.id, label: 'Websites', value: '10', sortOrder: 2 },
  { id: 5, productId: PLAN.id, label: 'Free SSL', value: 'Yes', sortOrder: 3 },
  { id: 6, productId: PLAN_PRO.id, label: 'Free SSL', value: 'Yes', sortOrder: 3 },
]

/** The enabled billing currencies: USD is the base one, so a guest is priced in it until they pick AMD. */
export const CURRENCIES = [
  { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true },
  { code: 'AMD', numeric: '051', prefix: '', suffix: ' AMD', decimals: 0, isBase: false },
]

/**
 * The catalogue as `GET /api/public/products?currency=` prices it: every product's `pricing` is its stored
 * price in the requested currency (the base one when the code is missing or unknown).
 *
 * @param {string | null} code Requested ISO 4217 code, or null for the payer's default.
 * @returns {object[]} The products, priced in that currency.
 */
export const productsIn = (code) =>
  PRODUCTS.map((product) => {
    const price = product.prices.find((p) => p.currencyCode === code) ?? product.prices[0]
    return { ...product, pricing: { monthly: price.monthly, annual: price.annual } }
  })

/** Domain that the stub reports as already registered. */
export const TAKEN_DOMAIN = 'taken-e2e.com'

/** Search term whose `.com` result is available (the stub answers "available" for every name but {@link TAKEN_DOMAIN}). */
export const FREE_TERM = 'fresh-e2e'

/** TLD price table served by `GET /api/domains/tld-pricing`. */
export const TLD_PRICING = {
  currency: { code: 'USD', prefix: '$' },
  pricing: [
    {
      tld: 'com',
      sellCurrency: 'USD',
      register: [{ years: 1, amount: 10 }],
      transfer: [{ years: 1, amount: 10 }],
      renew: [{ years: 1, amount: 12 }],
      categories: ['popular'],
    },
    {
      tld: 'net',
      sellCurrency: 'USD',
      register: [{ years: 1, amount: 12 }],
      transfer: [{ years: 1, amount: 12 }],
      renew: [{ years: 1, amount: 14 }],
      categories: ['popular'],
    },
  ],
}

/** TLD price table priced in AMD, served when `GET /api/domains/tld-pricing?currency=AMD` is asked. */
export const TLD_PRICING_AMD = {
  currency: { code: 'AMD', prefix: '' },
  pricing: [
    { ...TLD_PRICING.pricing[0], sellCurrency: 'AMD', register: [{ years: 1, amount: 4000 }], transfer: [{ years: 1, amount: 4000 }], renew: [{ years: 1, amount: 4800 }] },
    { ...TLD_PRICING.pricing[1], sellCurrency: 'AMD', register: [{ years: 1, amount: 4800 }], transfer: [{ years: 1, amount: 4800 }], renew: [{ years: 1, amount: 5600 }] },
  ],
}

/** The signed-in customer, as `GET /api/clients/me` returns them. */
export const CLIENT = {
  id: 1,
  firstName: 'Eve',
  lastName: 'Tester',
  companyName: null,
  email: 'e2e@example.com',
  phone: null,
  address: { street: null, address2: null, city: null, state: null, postCode: null, country: 'US' },
  language: null,
  currency: 'USD',
  paymentMethod: 'stripe',
  notifications: {
    general: true,
    invoice: true,
    support: true,
    product: true,
    domain: true,
    affiliate: false,
    marketingOptIn: false,
    statusUpdate: false,
  },
  twoFactorEnabled: false,
  contacts: [],
}

/** Credentials the stub accepts on sign-in. */
export const CLIENT_CREDENTIALS = { email: CLIENT.email, password: 'correct-horse' }

/**
 * A second customer whose account has two-factor on: the stub answers their password step with a
 * challenge instead of a session, and finishes it on {@link TWO_FACTOR_LOGIN_CODE}.
 */
export const TWO_FACTOR_CREDENTIALS = { email: 'two-factor@example.com', password: 'correct-horse' }

/** The challenge token the stub hands out with the two-factor sign-in step. */
export const TWO_FACTOR_PENDING_TOKEN = 'e2e-pending-token'

/** The only code the stub accepts on the second sign-in step. */
export const TWO_FACTOR_LOGIN_CODE = '654321'

/** The unpaid invoice the pay flow settles. */
export const INVOICE = {
  id: 501,
  status: 'Unpaid',
  currency: 'USD',
  invoiceDate: '2030-01-01T00:00:00Z',
  dueDate: '2030-01-15T00:00:00Z',
  createdAt: '2030-01-01T00:00:00Z',
  paidAt: null,
  total: 5,
  amountDue: 5,
  subTotal: 5,
  tax: 0,
  taxRate: 0,
  credit: 0,
  paymentMethod: null,
  items: [{ id: 1, description: 'E2E Starter - monthly', unitPrice: 5, quantity: 1, amount: 5 }],
  transactions: [],
}

/** The saved card the pay page preselects, in the camelCase shape the backend sends. */
export const SAVED_CARD = {
  id: 'pm_e2e',
  type: 'Card',
  brand: 'visa',
  last4: '4242',
  expiryMonth: 12,
  expiryYear: 2030,
  isDefault: false,
}

/** Operator settings the storefront reads; a light default and an uploaded light/dark logo pair. */
export const PUBLIC_SETTINGS = [
  { key: 'portal.template', value: 'aurora' },
  { key: 'portal.theme.default', value: 'light' },
  { key: 'portal.theme.user_toggle', value: 'true' },
  { key: 'portal.logo', value: '/logo.svg' },
  { key: 'portal.logo.dark', value: '/logo-dark.svg' },
  // Every home section on and every figure, switch and response time set, as the demo seed does.
  { key: 'portal.sections.stats', value: 'true' },
  { key: 'portal.sections.partners', value: 'true' },
  { key: 'portal.sections.services', value: 'true' },
  { key: 'portal.sections.process', value: 'true' },
  { key: 'portal.sections.plans', value: 'true' },
  { key: 'portal.sections.comparison', value: 'true' },
  { key: 'portal.sections.dashboard', value: 'true' },
  { key: 'portal.sections.use_cases', value: 'true' },
  { key: 'portal.sections.migration', value: 'true' },
  { key: 'portal.sections.projects', value: 'true' },
  { key: 'portal.sections.testimonials', value: 'true' },
  { key: 'portal.sections.faq', value: 'true' },
  { key: 'portal.sections.cta', value: 'true' },
  { key: 'portal.sections.locations', value: 'true' },
  { key: 'portal.sections.performance', value: 'true' },
  { key: 'portal.sections.security', value: 'true' },
  { key: 'portal.sections.apps', value: 'true' },
  { key: 'portal.sections.guarantees', value: 'true' },
  { key: 'portal.sections.support', value: 'true' },
  { key: 'portal.sections.trusted', value: 'true' },
  { key: 'portal.stats.response_time', value: '180 ms' },
  { key: 'portal.stats.sites_hosted', value: '12,000+' },
  { key: 'portal.stats.support_response', value: '15 min' },
  { key: 'portal.stats.uptime', value: '99.9%' },
  { key: 'portal.features.ssl', value: 'true' },
  { key: 'portal.features.ddos', value: 'true' },
  { key: 'portal.features.backups', value: 'true' },
  { key: 'portal.features.malware', value: 'true' },
  { key: 'portal.features.firewall', value: 'true' },
  { key: 'portal.installer.app_count', value: '150+' },
  { key: 'portal.guarantee.money_back_days', value: '30' },
  { key: 'portal.guarantee.no_hidden_fees', value: 'true' },
  { key: 'portal.migration.free', value: 'true' },
  { key: 'portal.support.response.chat', value: '1 min' },
  { key: 'portal.support.response.ticket', value: '1 hour' },
  { key: 'portal.support.response.email', value: '4 hours' },
  { key: 'portal.support.response.phone', value: '5 min' },
]

/** What the contact form submits, reused by the spec to fill the form and to check what arrived. */
export const CONTACT = {
  name: 'Eve Tester',
  email: 'e2e@example.com',
  message: 'Hello from the end-to-end suite.',
}

/** Two published knowledge-base articles; the first carries markup the page must neutralise. */
export const KB_ARTICLES = [
  {
    id: 31,
    title: 'How to reset your password',
    content: '<p>Open <strong>account settings</strong> and choose a new password.</p>'
      + '<script>window.__kbXss = 1</script>'
      + '<img src="x" onerror="window.__kbXss = 2">'
      + '<a href="javascript:window.__kbXss = 3">bad link</a>',
    category: 'Account',
    isPublished: true,
  },
  {
    id: 32,
    title: 'Update your billing details',
    content: '<p>Billing details live under the account page.</p>',
    category: 'Billing',
    isPublished: true,
  },
]

/** One published announcement whose body also carries a script, to prove it is stripped. */
export const ANNOUNCEMENT = {
  id: 7,
  title: 'Scheduled maintenance window',
  content: '<p>We will <em>upgrade</em> the platform on Sunday.</p><script>window.__newsXss = 1</script>',
  publishedAt: '2030-03-05T09:00:00Z',
}

/** Support departments a new ticket can be routed to. */
export const DEPARTMENTS = [
  { id: 1, name: 'Technical Support' },
  { id: 2, name: 'Sales' },
]

/** What the new-ticket spec types. */
export const NEW_TICKET = {
  subject: 'E2E: my site is slow',
  message: 'The homepage takes ten seconds to load.',
}

/** Reply the ticket spec posts onto an open ticket. */
export const TICKET_REPLY = 'Thanks, this is still happening.'

/** The open ticket the stub starts with (restored on every reset), as the list endpoint returns it (`MyTicketListItemDto`). */
export const SEEDED_TICKET = {
  id: 801,
  subject: 'Seeded e2e ticket',
  status: 'Open',
  priority: 'Medium',
  createdAt: '2030-02-01T10:00:00Z',
  replyCount: 1,
  departmentName: 'Technical Support',
  lastReplyAt: '2030-02-01T11:00:00Z',
}

/** The opening message and the staff answer of {@link SEEDED_TICKET} (`TicketDto.Message` and `TicketReplyDto`). */
export const SEEDED_TICKET_THREAD = {
  message: 'Seeded opening message.',
  replies: [
    { id: 1, message: 'Seeded staff answer.', authorName: 'Support Agent', isStaffReply: true, createdAt: '2030-02-01T11:00:00Z' },
  ],
}

/** A hosting service that is already set up (it has a control-panel user). */
export const SERVICE = {
  id: 21,
  productId: PLAN.id,
  productName: PLAN.name,
  billingCycle: 'monthly',
  status: 'Active',
  nextRenewalAt: '2030-02-01T00:00:00Z',
  domain: 'e2e-site.com',
  username: 'e2euser',
  price: 5,
  firstPaymentAmount: 5,
  paymentMethod: null,
  server: null,
  createdAt: '2030-01-01T00:00:00Z',
  suspensionSource: null,
  usage: null,
}

/** A registered domain, as `GET /api/me/domains` returns it. */
export const DOMAIN = {
  id: 41,
  name: 'e2e-site.com',
  status: 'Active',
  registeredAt: '2029-02-01T00:00:00Z',
  expiresAt: '2030-02-01T00:00:00Z',
  autoRenew: true,
  whoisPrivacy: false,
  isLocked: true,
  firstPaymentAmount: 10,
  recurringAmount: 12,
  paymentMethod: null,
  priceCurrency: 'USD',
  nextDueDate: '2030-02-01T00:00:00Z',
  registrationPeriod: 1,
  management: { dnsManagement: false, emailForwarding: false, nameservers: [], dnsRecords: [], emailForwardingRules: [] },
}

/** Invoice ids the stub treats as the three hosted-gateway outcomes (any other id is unknown to it). */
export const GATEWAY_PAID_INVOICE = '501'

/** The invoice id whose hosted-gateway payment is still being confirmed. */
export const GATEWAY_PENDING_INVOICE = '502'

/** The invoice id whose hosted-gateway payment was declined. */
export const GATEWAY_DECLINED_INVOICE = '503'

/** Email the register spec uses to make the stub refuse the form with a field error. */
export const TAKEN_EMAIL = 'taken@e2e.test'

/** The sentence the stub puts on the refused email field (a server sentence the page must show verbatim). */
export const TAKEN_EMAIL_MESSAGE = 'E2E: this email is already registered.'

/** Email the contact spec uses to make the stub refuse the form with a field error. */
export const BAD_CONTACT_EMAIL = 'bad-contact@e2e.test'

/** The sentence the stub puts on the refused contact email field. */
export const BAD_CONTACT_EMAIL_MESSAGE = 'E2E: this email address is not accepted.'

/** A new profile name the account spec saves. */
export const NEW_FIRST_NAME = 'Evelyn'

/** The disk and bandwidth figures the server last reported for {@link SERVICE_MANAGED}, both with a limit so both bars show. */
export const SERVICE_USAGE = {
  diskUsedMb: 512,
  diskLimitMb: 2048,
  bandwidthUsedMb: 1024,
  bandwidthLimitMb: 10240,
  updatedAt: '2030-01-15T00:00:00Z',
}

/**
 * A hosting service that is already set up and has NO domain.
 *
 * The backend resolves the live nameservers and certificate of a service's domain (a DNS lookup
 * and a TLS handshake), which a spec must not depend on the network for. A service without a
 * domain returns an empty answer without looking anything up. Every spec that opens a service's
 * detail page therefore uses this one (or {@link SERVICE_PENDING}), never {@link SERVICE}.
 */
export const SERVICE_MANAGED = {
  id: 22,
  productId: PLAN_PRO.id,
  productName: PLAN_PRO.name,
  billingCycle: 'monthly',
  status: 'Active',
  nextRenewalAt: '2030-02-01T00:00:00Z',
  domain: null,
  username: 'e2emgd',
  price: 15,
  firstPaymentAmount: 15,
  paymentMethod: null,
  server: null,
  createdAt: '2030-01-01T00:00:00Z',
  suspensionSource: null,
  usage: SERVICE_USAGE,
}

/** What the stub answers for the managed service's SSH access; the username and command are filled from the service. */
export const SSH_ACCESS = {
  state: 'Available',
  hostname: 'cwp.e2e.test',
  port: 2222,
  maxKeys: 20,
}

/** A throwaway ED25519 public key (no private half exists anywhere in the suite). */
export const SSH_PUBLIC_KEY = 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG6EJIHRxsxge2aZnl+clPBfoX0nmvD7FfHXM2QQ3+3b e2e@laptop'

/** A paid hosting service that has no control-panel account yet, so it needs the setup wizard (no domain, for the same reason as {@link SERVICE_MANAGED}). */
export const SERVICE_PENDING = {
  id: 23,
  productId: PLAN.id,
  productName: PLAN.name,
  billingCycle: 'monthly',
  status: 'Pending',
  nextRenewalAt: '2030-02-01T00:00:00Z',
  domain: null,
  username: null,
  price: 5,
  firstPaymentAmount: 5,
  paymentMethod: null,
  server: null,
  createdAt: '2030-01-01T00:00:00Z',
  suspensionSource: null,
  usage: null,
}

/** A hosting service suspended for an overdue invoice (no domain, for the same reason as {@link SERVICE_MANAGED}). */
export const SERVICE_SUSPENDED = {
  id: 24,
  productId: PLAN.id,
  productName: PLAN.name,
  billingCycle: 'monthly',
  status: 'Suspended',
  nextRenewalAt: '2030-02-01T00:00:00Z',
  domain: null,
  username: 'e2esusp',
  price: 5,
  firstPaymentAmount: 5,
  paymentMethod: null,
  server: null,
  createdAt: '2030-01-01T00:00:00Z',
  suspensionSource: 'Overdue',
  usage: null,
}

/** What the setup wizard types: a valid hostname, a username of 3-8 letters/digits, a password of 8+ characters. */
export const SETUP = {
  domain: 'wizard-e2e.com',
  username: 'wizuser',
  password: 'wizard-pass-1',
}

/** The new control-panel password the service spec sets. */
export const NEW_SERVICE_PASSWORD = 'fresh-pass-123'

/** The reason the cancellation spec gives. */
export const CANCEL_REASON = 'E2E: moving to another plan.'

/** The invoice raised against {@link SERVICE_MANAGED}, as `GET /api/me/services/:id/invoices` lists it. */
export const SERVICE_INVOICE = {
  id: INVOICE.id,
  invoiceDate: INVOICE.invoiceDate,
  dueDate: INVOICE.dueDate,
  total: INVOICE.total,
  status: INVOICE.status,
}

/** The nameservers the domain starts with, as `GET /api/me/domains/:id` carries them in `management.nameservers`. */
export const DOMAIN_NAMESERVERS = [
  { id: 1, host: 'ns1.e2e-site.test' },
  { id: 2, host: 'ns2.e2e-site.test' },
]

/** The custom nameservers the domain spec saves. */
export const NEW_NAMESERVERS = ['ns1.fresh-e2e.test', 'ns2.fresh-e2e.test']

/** The authorisation code the stub issues on request. */
export const EPP_CODE = 'EPP-E2E-CODE'

/** What the domain transfer spec types. */
export const TRANSFER = {
  domain: 'moved-e2e.com',
  eppCode: 'EPP-E2E-1',
}

/** Order the stub answers every order placement with (order, its invoice, the payment token and what it bills). */
export const ORDER = {
  orderId: 301,
  invoiceId: INVOICE.id,
  paymentToken: 'tok-e2e',
  total: PLAN.pricing.monthly,
  currency: 'USD',
}

/** Order raised by a domain renewal or transfer; the invoice is the unpaid one. */
export const DOMAIN_ORDER = {
  orderId: 701,
  invoiceId: INVOICE.id,
}

/** Payment gateways enabled for a new order: bank transfer only, because it needs no card element or bank redirect. */
export const GATEWAY_METHODS = [
  { module: 'bank_transfer', displayName: 'Bank transfer' },
]

/** The enrolment the stub answers `POST /api/me/2fa/setup` with. */
export const TWO_FACTOR = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUri: 'otpauth://totp/E2E:e2e@example.com?secret=JBSWY3DPEHPK3PXP&issuer=E2E',
  code: '123456',
}

/** The sentence the stub puts on a refused two-factor code. */
export const TWO_FACTOR_REFUSED = 'E2E: this code is not valid.'

/*
 * NOTE for the contacts, team and WHOIS fixtures below: they are the backend's own wire shapes
 * (`ContactDto`, `ClientUserDto`, `WhoisDto`, camelCase), which the BFF forwards unmapped, so the
 * client area reads exactly these member names.
 */

/** The contact the account starts with, as `GET /api/clients/me/contacts` lists it (`ContactDto`). */
export const SEEDED_CONTACT = {
  id: 61,
  firstName: 'Carl',
  lastName: 'Contact',
  companyName: null,
  email: 'carl@e2e.test',
  phone: null,
  type: 'General',
  address: { street: null, address2: null, city: null, state: null, postCode: null, country: null },
  notifications: { general: true, invoice: true, support: true, product: true, domain: false, affiliate: false },
}

/** The contact the spec adds. */
export const NEW_CONTACT = {
  firstName: 'Nina',
  lastName: 'Newcontact',
  email: 'nina@e2e.test',
}

/** The first name the contact edit saves. */
export const EDITED_CONTACT_FIRST_NAME = 'Carla'

/**
 * The signed-in person's account, as `GET /api/auth/me` answers (`MyAccountDto`). The portal reads
 * `clientPermissions` from it to decide which sections to show, and `clientId` / `externalAccount`
 * to know whether the person acts for an account at all; the owner of a client record holds every
 * flag, so every section is visible to the flows.
 */
export const ACCOUNT = {
  email: CLIENT.email,
  roles: ['Client'],
  emailVerified: true,
  clientPermissions: 8191,
  clientId: CLIENT.id,
  externalAccount: null,
}

/**
 * `GET /api/auth/me` fields of a member of the account who holds only the read-only email flag: no
 * right to place orders and none to view or pay invoices (`ClientPermission.ViewEmails` is 2048).
 */
export const MEMBER_WITHOUT_PURCHASE_ACCOUNT = {
  clientPermissions: 2048,
}

/**
 * `GET /api/auth/me` fields of a signed-in first-time buyer: no client record and no external
 * account yet (the first order creates the account), so no permission flag is held.
 */
export const FIRST_TIME_BUYER_ACCOUNT = {
  clientPermissions: 0,
  clientId: null,
  externalAccount: null,
}

/** The account owner, listed first in the team and never removable (`ClientUserDto`). */
export const TEAM_OWNER = {
  userId: 'u-owner',
  firstName: 'Eve',
  lastName: 'Tester',
  email: CLIENT.email,
  isOwner: true,
  permissions: 8191,
  lastLoginAt: null,
  createdAt: '2029-01-01T00:00:00Z',
}

/** A team member the owner can remove (`ClientUserDto`). */
export const TEAM_MEMBER = {
  userId: 'u-member',
  firstName: 'Mia',
  lastName: 'Member',
  email: 'member@e2e.test',
  isOwner: false,
  permissions: 8191,
  lastLoginAt: null,
  createdAt: '2029-03-01T00:00:00Z',
}

/** The registry record of {@link DOMAIN}, as `GET /api/me/domains/:id/whois` answers (`WhoisDto`). */
export const WHOIS_RECORD = {
  registrar: 'E2E Registrar Ltd',
  registrant: 'Eve Tester',
  createdAt: '2029-02-01T00:00:00Z',
  expiresAt: '2030-02-01T00:00:00Z',
}

/** The registrant contact of {@link DOMAIN}, as `GET /api/me/domains/:id/contact` answers (`DomainContactDto`). */
export const REGISTRANT_CONTACT = {
  firstName: 'Reggie',
  lastName: 'Registrant',
  organization: null,
  email: 'registrant@e2e.test',
  phone: '+15550100001',
  address1: '1 Registry Street',
  address2: null,
  city: 'Yerevan',
  state: 'Yerevan',
  postalCode: '0001',
  country: 'AM',
}

/** What the invitation form submits. */
export const INVITE = {
  firstName: 'Ivy',
  lastName: 'Invitee',
  email: 'ivy@e2e.test',
}

/** How far ahead the stub reports the next demo reset, in milliseconds (two and a half hours). */
export const DEMO_RESET_IN_MS = 150 * 60 * 1000

/** The demo accounts the stub publishes; the admin one must never reach the customer sign-in page. */
export const DEMO_ACCOUNTS = [
  { role: 'Admin', email: 'admin@demo.test', password: 'demo-admin-pass' },
  { role: 'Client', email: 'client@demo.test', password: 'demo-client-pass' },
]

/** Questions the public FAQ feed serves; the topic is spelled by its enum name, as the API does. */
export const FAQS = [
  { id: 1, category: 'General', question: 'Example question one?', answer: 'Example answer one.', sortOrder: 1 },
  { id: 2, category: 'General', question: 'Example question two?', answer: 'Example answer two.', sortOrder: 2 },
  { id: 3, category: 'Products', question: 'Example question three?', answer: 'Example answer three.', sortOrder: 3 },
]

/** Testimonials the public feed serves. */
export const TESTIMONIALS = [
  { id: 1, name: 'Example Person', company: 'Example Co', role: 'Founder', avatarUrl: null, quote: 'Example quote.', rating: 5, sortOrder: 1 },
]

/** Partners the public feed serves. */
export const PARTNERS = [
  { id: 1, name: 'Example Co', logoUrl: null, url: 'https://example.test', sortOrder: 1 },
  { id: 2, name: 'Alderstone Group', logoUrl: '/demo/partners/alderstone-group.svg', url: null, sortOrder: 2 },
  { id: 3, name: 'Beacon Labs', logoUrl: '/demo/partners/beacon-labs.svg', url: null, sortOrder: 3 },
  { id: 4, name: 'Cobalt Works', logoUrl: '/demo/partners/cobalt-works.svg', url: null, sortOrder: 4 },
  { id: 5, name: 'Delta Studio', logoUrl: '/demo/partners/delta-studio.svg', url: null, sortOrder: 5 },
  { id: 6, name: 'Ember Foods', logoUrl: '/demo/partners/ember-foods.svg', url: null, sortOrder: 6 },
  { id: 7, name: 'Fjord Logistics', logoUrl: '/demo/partners/fjord-logistics.svg', url: null, sortOrder: 7 },
  { id: 8, name: 'Granite Bank', logoUrl: '/demo/partners/granite-bank.svg', url: null, sortOrder: 8 },
]

/** Server locations the public feed serves. */
export const LOCATIONS = [
  { id: 1, countryCode: 'DE', name: 'Example location one', city: 'Example City', latencyNote: '20 ms' },
  { id: 2, countryCode: 'US', name: 'Example location two', city: null, latencyNote: null },
]

/** Projects the public feed serves. */
export const PROJECTS = [
  { id: 1, slug: 'example-project', title: 'Example project', summary: null, description: null, imageUrls: [], url: 'https://example.test', sortOrder: 1 },
  { id: 2, slug: 'northwind-portal', title: 'Northwind Portal', summary: 'A customer portal with online payments.', description: null, imageUrls: ['/demo/projects/northwind-portal.svg'], url: null, sortOrder: 2 },
  { id: 3, slug: 'bluebird-shop', title: 'Bluebird Shop', summary: 'An online store with a fast checkout.', description: null, imageUrls: ['/demo/projects/bluebird-shop.svg'], url: null, sortOrder: 3 },
  { id: 4, slug: 'harborline-tracking', title: 'Harborline Tracking', summary: 'A shipment tracking dashboard.', description: null, imageUrls: ['/demo/projects/harborline-tracking.svg'], url: null, sortOrder: 4 },
]

/**
 * The home-page slides the public feed serves when a spec switches them on: one with an uploaded
 * cover picture and one with none, so the slider is checked on both kinds of cover.
 */
export const SLIDES = [
  {
    id: 1,
    iconName: 'server',
    brandColor: '#2563eb',
    imageUrl: '/demo/slides/cloud-vps.svg',
    productId: null,
    content: { title: 'Example slide with a photograph', tagline: 'Fast hosting', description: 'A slide whose background is an image under a dark scrim.', features: ['Free SSL', 'Daily backups'] },
    links: { demoUrl: null, learnMoreUrl: '/products' },
    callToAction: { text: null, url: null },
    pricing: { monthlyPrice: null, annualPrice: null },
  },
  {
    id: 2,
    iconName: 'web',
    brandColor: '#0d9488',
    imageUrl: '',
    productId: null,
    content: { title: 'Example slide without an image', tagline: 'Plain background', description: 'A slide with no image follows the active colour mode.', features: [] },
    links: { demoUrl: null, learnMoreUrl: '/vps' },
    callToAction: { text: null, url: null },
    pricing: { monthlyPrice: null, annualPrice: null },
  },
]
