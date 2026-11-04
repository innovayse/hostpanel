---
title: Rate limits
---

# Rate limits

Every request is counted against a global budget. Endpoints that take a credential, call another company's system or send something out of the install are also counted against a tighter named policy. When you run out, the API refuses the call with a `429` until the budget is free again.

## Policies

Each budget is a number of requests in a sliding window, counted per signed-in user, or per caller address for a request with no credential. The window slides in six steps, so a part of the budget comes back every few seconds rather than all at once on a boundary.

<SettingTable :settings="[
  { name: 'global', type: 'budget', default: '1200 per 60 s', description: 'Every request, whatever its endpoint, except the health probe.' },
  { name: 'auth', type: 'budget', default: '15 per 60 s', description: 'Endpoints that take a credential, a one-time code or a reset token, where a fast rate means guessing.' },
  { name: 'upstream', type: 'budget', default: '60 per 60 s', description: 'Endpoints where each call costs a call to a third party, such as the domain registrar or a WHOIS server.' },
  { name: 'strict', type: 'budget', default: '5 per 60 s', description: 'Endpoints that send something out of the install, such as the contact form and guest checkout.' },
  { name: 'ssh-keys', type: 'budget', default: '20 per 60 s', description: 'Adding and removing the SSH keys of a hosting account. Each call opens an SSH connection to a managed server.' },
  { name: 'concurrent', type: 'in flight', default: '5 at once', description: 'Operations that are costly for what they hold, such as report exports and migration runs. Counted by how many run at the same time, not per minute.' },
  { name: 'plugin-default', type: 'budget', default: '120 per 60 s', description: 'A plugin route that names no tier. Counted per plugin and per caller.' },
  { name: 'plugin-strict', type: 'budget', default: '20 per 60 s', description: 'A plugin route that is costly or sensitive, such as one that sends messages. Counted per plugin and per caller.' },
  { name: 'plugin-heavy', type: 'budget', default: '6 per 60 s', description: 'A plugin route that does heavy work and is called rarely, such as a report or an export. Counted per plugin and per caller.' },
  { name: 'plugin-stream', type: 'in flight', default: '3 at once', description: 'Streaming plugin routes one caller may hold open at once, per plugin. Counted on top of the tier of the route.' },
]" />

Both budgets apply. A call to an endpoint with a named policy is counted against that policy and against `global`, and the tighter one decides. Nothing waits in a queue: a call over the budget is refused at once.

### Which endpoints use each policy

This list is read from the controllers of the API when the site is built. The plugin policies are not in it: the plugin call filter applies them to the routes that plugins declare.

<!--@include: ./_partials/rate-limit-endpoints.md-->

## When you are refused

A refused call answers `429 Too Many Requests` with the standard [error body](./errors) and the code `HOSTPANEL_RATE_LIMITED`. Two headers tell you what to do:

<SettingTable :settings="[
  { name: 'Retry-After', type: 'response header', default: 'None', description: 'How many seconds to wait before you try again. At least 1. For a concurrency policy it is always 1: a slot frees when another call finishes.' },
  { name: 'X-RateLimit-Remaining', type: 'response header', default: '0', description: 'How many calls are left in the budget that refused you. Always 0 on a 429.' },
]" />

<Callout type="warning">
Respect <code>Retry-After</code>. Trying again at once goes straight back into the same refusal.
</Callout>

Back off for the number of seconds the header gives, then try again. Spread bulk work over time instead of sending it in a burst.

## Behind a proxy

Limits count per caller address, so the API has to know which address is the caller's when a proxy stands in front of it. It reads the address from `X-Forwarded-For` only as far as the number of trusted proxies it has been told about, and only when the request comes from a proxy it knows.

<SettingTable :settings="[
  { name: 'RATE_LIMIT_TRUSTED_PROXY_COUNT', type: 'number', default: '1', description: 'How many trusted proxies stand between a caller and the API. The shipped stacks set 1, the bundled edge proxy. The API itself starts from 0, which believes no forwarded header. Add 1 for each extra proxy you put in front.' },
]" />

See [Reverse proxy and trusted proxies](../operators/configuration#reverse-proxy-and-trusted-proxies) for the whole setup, including `RateLimit__KnownProxies__N` and `RateLimit__KnownNetworks__N`.

## Settings

The budgets above are defaults. An operator changes them on the API service, in a compose override. The settings are listed, with their defaults and ranges, in [Rate-limit settings](../operators/configuration#rate-limit-settings-compose-override-only). A tier that is larger than `global`, or a value that is not positive, stops the API from starting.

## Sign-in lockout

Sign-in lockout is separate from the rate limits. After too many wrong passwords in a row, the account is locked and every sign-in answers `SIGN_IN_ACCOUNT_LOCKED` with status `423`, whatever password is sent, until the lockout ends.

<SettingTable :settings="[
  { name: 'SignInLockout__MaxFailedAttempts', type: 'number', default: '5', description: 'How many wrong passwords in a row lock the account. From 1 to 100.' },
  { name: 'SignInLockout__LockoutMinutes', type: 'number', default: '15', description: 'How long a locked account refuses every password, in minutes. From 1 to 1440.' },
]" />

The count is kept on the account, so it holds across restarts and across every instance of the API. A successful sign-in clears it.
