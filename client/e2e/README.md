# hostpanel client — end-to-end suite

Playwright drives a real browser against the real Nuxt server. Only the C# API is replaced, by
`support/stub-backend.mjs` — a real HTTP server, because the storefront is server-rendered and the
calls Nitro makes while rendering are invisible to `page.route`.

## Flows (one spec each, `data-testid` selectors only)

| Spec | Flow |
|---|---|
| `hosting-to-cart.spec.ts` | home -> hosting pricing -> choose plan -> configure -> cart |
| `domain-search.spec.ts` | search a name; available can be added, taken cannot |
| `contact-form.spec.ts` | fill and submit; success shown and the backend received the enquiry |
| `sign-in.spec.ts` | valid credentials open the dashboard; a wrong password is refused |
| `invoice-pay.spec.ts` | dashboard -> invoice -> pay page -> pay with the saved card |
| `theme-toggle.spec.ts` | light/dark toggle swaps palette and logo and persists across reload |
| `language-switch.spec.ts` | en/ru/hy contact form: `html lang` and the `X-Language` the backend received |
| `contact-field-errors.spec.ts` | a refused contact email is marked invalid with the backend sentence |
| `knowledgebase.spec.ts` | category -> article, rich text kept and script/handler/`javascript:` removed; search |
| `announcements.spec.ts` | list -> announcement, body sanitised |
| `faq.spec.ts` | topic accordion opens, closes, leads back |
| `auth-gate.spec.ts` | signed-out gate returns to the requested page; invitation link states and acceptance |
| `register-forgot.spec.ts` | registration field refusal; password recovery request |
| `client-area-states.spec.ts` | no-profile notice, backend-down error state, invoice list -> invoice |
| `payment-result.spec.ts` | hosted-gateway return: paid, pending, declined, missing, unknown |
| `tickets.spec.ts` | list -> thread, reply, new ticket |
| `services-domains.spec.ts` | services and domains lists; the needs-setup marker |
| `service-detail.spec.ts` | service overview, password change, confirmed cancellation, pending service sent to the wizard |
| `service-setup.spec.ts` | setup wizard: step validation, finish, backend refusal, already set up |
| `domain-detail.spec.ts` | domain overview, auto-renew, lock and ID protection (incl. a refused change), authorisation code, nameservers, renewal order |
| `domain-transfer.spec.ts` | public transfer page hand-off, transfer order, refusal |
| `product-detail.spec.ts` | product page: plans and prices, add to cart, not-found |
| `order-placement.spec.ts` | checkout by bank transfer: order body, invoice, refused order |
| `session-expiry.spec.ts` | 401 on a client-side step -> sign-in form -> signing in again |
| `account.spec.ts` | edit and save the profile; unreadable-profile state |
| `account-payment-methods.spec.ts` | saved card: list, set default, confirmed removal |
| `account-two-factor.spec.ts` | enrol (secret, QR, code), refused code, disable |
| `account-team.spec.ts` | owner vs member, confirmed removal, invitation, refusal |
| `account-contacts.spec.ts` | contact add, nameless refusal, edit, confirmed removal |
| `storefront-templates.spec.ts` | home in aurora/nova/classic; seeded cart -> checkout summary in each |

Not covered: a card order (needs the payment provider's element), the domain WHOIS-contact panel,
and an address tab (the account page has none). The service detail and setup specs use services
that have no domain (`SERVICE_MANAGED`, `SERVICE_PENDING`): the BFF resolves the live DNS and
certificate of a service's domain, and for a service without one it answers empty without looking
anything up, which keeps the specs off the network.

## Running

```bash
yarn build        # the suite serves the production build (.output) by default
yarn e2e          # starts the stub backend and the client itself, then runs the specs
E2E_DEV=1 yarn e2e   # serve `nuxt dev` instead — no build needed, slower first render
```

Ports: `E2E_PORT` (client, 3100) and `E2E_STUB_PORT` (stub backend, 5999). The client runs in
`local` auth mode (`AUTH_MODE`, `NUXT_PUBLIC_AUTH_MODE`) with `API_URL` pointed at the stub.
Browsers are installed once with `npx playwright install chromium`.

## Fixtures

`support/fixtures.mjs` is the single source for what the stub serves and what the specs assert on.
`GET /__e2e/requests` on the stub lists the requests it received (method, path, body and the
`X-Language` it carried), so a spec can prove a submission reached the backend in the right
language. The other control endpoints are scriptable per test and cleared by `POST /__e2e/reset`
(also the log and everything a spec changed: tickets, profile, services, the domain, payment methods, contacts, the team, the two-factor flag): `POST /__e2e/script` makes one route answer a
refusal (`scriptStub` in `support/helpers.ts`), `POST /__e2e/settings` overrides a public portal
setting such as `portal.template` (`setPortalSetting`), `POST /__e2e/demo` turns demo mode on
(`enableDemoMode`; it is off by default), `POST /__e2e/plugin-slots` lists fake plugin widgets per
slot (`setPluginSlots`; every slot is empty by default).

The client area is entered by signing in through the BFF's own sign-in route with the stub
customer's credentials (`signIn` in `support/helpers.ts`), which leaves the `portal_session` and
`authed` cookies in the context; only `sign-in.spec.ts` goes through the form.

## No internet

Specs import `test` and `expect` from `support/test.ts`, not from `@playwright/test`. Its automatic
`offlineOnly` fixture aborts every request that does not go to the app (`E2E_PORT`) or the stub
backend (`E2E_STUB_PORT`) on localhost, so third-party maps, analytics, fonts and live chat can never
stall `networkidle` in a container.
