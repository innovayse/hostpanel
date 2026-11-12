/**
 * The one place the browser is handed to an address outside this site.
 *
 * A payment gateway's address comes from the backend, which got it from a plugin. It is data, not
 * code, so it is checked here before the browser follows it: only an absolute `https` address
 * qualifies, plus `http` while the app runs in development, where a local stand-in gateway has no
 * certificate. `javascript:`, `data:` and relative or malformed values are refused.
 *
 * @module utils/externalRedirect
 */

import { HTTPS_PREFIX } from '~/constants/urlScheme'
import type { GatewayStart } from '~/types/gatewaystart'
import { GatewaySessionKind } from '~/types/gatewaysessionkind'
import type { PaymentFormPost } from '~/types/paymentformpost'

/** Scheme prefix of an insecure address, accepted in development only. */
const HTTP_PREFIX = 'http://'

/**
 * Whether the browser may be handed to an address.
 *
 * @param target - The address the backend returned.
 * @param allowHttp - Whether `http` is acceptable; true only in development.
 * @returns True when the address is absolute and uses an accepted scheme.
 */
export const isSafeRedirectUrl = (target: string, allowHttp: boolean = Boolean(import.meta.dev)): boolean => {
  let parsed: URL
  try {
    parsed = new URL(target)
  } catch {
    return false
  }
  const prefix = `${parsed.protocol}//`
  return prefix === HTTPS_PREFIX || (allowHttp && prefix === HTTP_PREFIX)
}

/**
 * Hands the browser to an external address, when it is a safe one.
 *
 * @param target - The address to leave for.
 * @returns True when the browser was handed over; false when the address was refused and nothing happened.
 */
export const redirectToExternal = (target: string): boolean => {
  if (!isSafeRedirectUrl(target)) {
    return false
  }
  window.location.href = target
  return true
}

/**
 * Whether the browser may be handed to what the backend answered a payment start with. A form post is
 * safe when its action is a safe address, because that is where the browser sends the fields; every
 * other kind is safe when its page address is.
 *
 * @param start - The answer to a payment start.
 * @param allowHttp - Whether `http` is acceptable; true only in development.
 * @returns True when the form or the address may be followed.
 */
export const isSafeGatewayStart = (start: GatewayStart, allowHttp: boolean = Boolean(import.meta.dev)): boolean =>
  start.kind === GatewaySessionKind.FormPost
    ? start.formPost !== null && isSafeRedirectUrl(start.formPost.action, allowHttp)
    : isSafeRedirectUrl(start.redirectUrl, allowHttp)

/**
 * Posts a form to a gateway from a form the page builds and submits itself. Names and values are set as
 * properties of the elements, never written as markup, so nothing a plugin supplied can become markup
 * or script.
 *
 * @param formPost - The form to post; its action has been checked by {@link isSafeGatewayStart}.
 */
const postToGateway = (formPost: PaymentFormPost): void => {
  const form = document.createElement('form')
  form.method = 'post'
  form.action = formPost.action
  form.hidden = true
  for (const field of formPost.fields) {
    const input = document.createElement('input')
    input.type = 'hidden'
    input.name = field.name
    input.value = field.value
    form.append(input)
  }
  document.body.append(form)
  form.submit()
}

/**
 * Takes the browser to the gateway's hosted step the way the backend described: a redirect and a browser
 * step open the address, a form post submits the form. A browser step needs nothing more here; the
 * payer comes back to the result page, which hands the return request to the payment plugin.
 *
 * @param start - The answer to a payment start.
 * @returns True when the browser was handed over; false when the answer was refused and nothing happened.
 */
export const redirectToGateway = (start: GatewayStart): boolean => {
  if (!isSafeGatewayStart(start)) {
    return false
  }
  if (start.kind === GatewaySessionKind.FormPost && start.formPost) {
    postToGateway(start.formPost)
    return true
  }
  window.location.href = start.redirectUrl
  return true
}
