/**
 * The sign-in transaction: everything one authorization-code attempt needs to be completed, kept in
 * one sealed cookie of its own.
 *
 * Each attempt (an interactive sign-in or a silent probe) gets a cookie named after its `state`
 * (`<prefix>sso_tx_<first 16 hex of sha256(state)>`), so attempts started in several tabs, or by a
 * probe while a person is still typing a password, never overwrite one another. The cookie holds
 * the `state`, the PKCE verifier, the page to return to, the kind of attempt and its start time,
 * sealed with AES-256-GCM under a key derived from the session encryption key, with the cookie name
 * as additional authenticated data. The browser can neither read nor edit it, and cannot move a
 * valid one to another name.
 *
 * The callback finds an attempt only through the `state` the provider returned, so a callback whose
 * `state` has no matching transaction is never accepted: that is the CSRF protection. A transaction
 * is single use: the callback deletes it as soon as it is found.
 *
 * Outside development the cookie name carries the `__Host-` prefix, which makes a browser accept it
 * only when it is Secure, has path `/` and no Domain attribute. The prefix needs Secure, so
 * development (plain HTTP) uses the bare name.
 *
 * @module server/utils/signon-transaction
 */

import { createHash, randomBytes, timingSafeEqual } from 'node:crypto'
import type { H3Event } from 'h3'
import {
  HOST_COOKIE_PREFIX,
  SSO_TRANSACTION_COOKIE_STEM,
  SSO_TRANSACTION_NAME_HASH_LENGTH
} from '~/constants/sessionCookie'
import { SSO_FLOW_COOKIE_MAX_AGE, SSO_MAX_LIVE_TRANSACTIONS, cookieOptions } from '~/server/utils/auth-cookie'
import { deriveKey, openText, readSessionKey, sealText } from '~/server/utils/session-crypto'
import { SignonTransactionKind } from '~/types/signontransactionkind'
import { SignonTransactionStatus } from '~/types/signontransactionstatus'

/** The version written into every sealed payload, so a later change of shape can be told apart. */
const TRANSACTION_VERSION = 1

/** The label the transaction key is derived under, so it is unrelated to the session key itself. */
const KEY_PURPOSE = 'signon-transaction'

/** Length in bytes of the key used when none is configured. */
const PROCESS_KEY_BYTES = 32

/** The input to start a transaction. */
export interface NewSignonTransaction {
  /** The anti-forgery `state` sent to the provider. */
  state: string
  /** The PKCE code verifier whose challenge was sent to the provider. */
  verifier: string
  /** The page to open afterwards, already validated, or null for the default. */
  returnTo: string | null
  /** Whether this is an interactive sign-in or a silent probe. */
  kind: SignonTransactionKind
}

/** The sealed payload of a transaction cookie. */
export interface SignonTransaction extends NewSignonTransaction {
  /** The payload version. */
  v: number
  /** When the attempt started, in milliseconds since the epoch. */
  createdAt: number
}

/** A lookup that found a live, authentic transaction. */
export interface FoundSignonTransaction {
  /** Marks the transaction as found. */
  status: SignonTransactionStatus.Found
  /** The transaction. */
  transaction: SignonTransaction
}

/** A lookup that found nothing usable. */
export interface MissingSignonTransaction {
  /** Why no usable transaction exists. */
  status: Exclude<SignonTransactionStatus, SignonTransactionStatus.Found>
  /** Nothing was found. */
  transaction: null
}

/** The result of looking a transaction up by the `state` the provider returned. */
export type SignonTransactionLookup = FoundSignonTransaction | MissingSignonTransaction

/** A transaction cookie the browser sent, opened. */
interface PresentTransaction {
  /** The cookie name. */
  name: string
  /** The transaction, or null when the cookie did not authenticate. */
  transaction: SignonTransaction | null
}

/** The key used when no session encryption key is configured (development, tests); lives for this process only. */
let processKey: Buffer | null = null

/**
 * The key transactions are sealed with.
 *
 * Derived from the configured session encryption key, which every replica shares, so any replica can
 * open what another sealed. When none is configured the sessions are kept in this process's memory
 * (the key is only required with Redis), so a key that lives for this process is just as wide.
 *
 * @returns A 32-byte key.
 * @throws Error when a configured key is malformed.
 */
const transactionKey = (): Buffer => {
  const configured = useRuntimeConfig().sessionEncryptionKey
  if (typeof configured === 'string' && configured.trim()) {
    const base = readSessionKey(configured)
    if (base) {
      return deriveKey(base, KEY_PURPOSE)
    }
  }
  processKey ??= randomBytes(PROCESS_KEY_BYTES)
  return processKey
}

/**
 * The cookie name prefix for this environment: `__Host-` everywhere but development.
 *
 * @returns The prefix, possibly empty.
 */
const namePrefix = (): string => (process.env.NODE_ENV === 'development' ? '' : HOST_COOKIE_PREFIX)

/**
 * The cookie name of the transaction for a `state`.
 *
 * @param state - The `state` of the attempt.
 * @returns `<prefix>sso_tx_<first 16 hex characters of sha256(state)>`.
 */
export const transactionCookieName = (state: string): string => {
  const digest = createHash('sha256').update(state).digest('hex').slice(0, SSO_TRANSACTION_NAME_HASH_LENGTH)
  return `${namePrefix()}${SSO_TRANSACTION_COOKIE_STEM}${digest}`
}

/**
 * Compares two strings in constant time.
 *
 * @param left - One value.
 * @param right - The other value.
 * @returns True when both are equal.
 */
const sameText = (left: string, right: string): boolean => {
  const leftBytes = Buffer.from(left)
  const rightBytes = Buffer.from(right)
  return leftBytes.length === rightBytes.length && timingSafeEqual(leftBytes, rightBytes)
}

/**
 * Whether a cookie name is one of the transaction cookies of this environment.
 *
 * @param name - A request cookie name.
 * @returns True for `<prefix>sso_tx_<hex>` of the expected length.
 */
const isTransactionCookieName = (name: string): boolean => {
  const head = `${namePrefix()}${SSO_TRANSACTION_COOKIE_STEM}`
  if (!name.startsWith(head)) {
    return false
  }
  const tail = name.slice(head.length)
  return tail.length === SSO_TRANSACTION_NAME_HASH_LENGTH && /^[0-9a-f]+$/.test(tail)
}

/**
 * Reads a sealed payload back into a transaction.
 *
 * @param text - The opened JSON.
 * @returns The transaction, or null when it is not a version-1 payload of the expected shape.
 */
const parseTransaction = (text: string): SignonTransaction | null => {
  try {
    const value = JSON.parse(text) as Record<string, unknown>
    const kinds: string[] = Object.values(SignonTransactionKind)
    if (
      value['v'] !== TRANSACTION_VERSION
      || typeof value['state'] !== 'string'
      || typeof value['verifier'] !== 'string'
      || (value['returnTo'] !== null && typeof value['returnTo'] !== 'string')
      || typeof value['kind'] !== 'string'
      || !kinds.includes(value['kind'])
      || typeof value['createdAt'] !== 'number'
    ) {
      return null
    }
    return value as unknown as SignonTransaction
  } catch {
    return null
  }
}

/**
 * Opens one transaction cookie by name.
 *
 * @param name - The cookie name, which is also the additional authenticated data.
 * @param sealed - The cookie value.
 * @returns The transaction, or null when the value does not authenticate or is malformed.
 */
const readTransaction = (name: string, sealed: string): SignonTransaction | null => {
  const text = openText(sealed, transactionKey(), name)
  return text === null ? null : parseTransaction(text)
}

/**
 * Whether a transaction is still within the lifetime of a sign-in attempt.
 *
 * @param transaction - The transaction.
 * @returns True when it started no more than `SSO_FLOW_COOKIE_MAX_AGE` seconds ago.
 */
const isLive = (transaction: SignonTransaction): boolean => {
  return Date.now() - transaction.createdAt <= SSO_FLOW_COOKIE_MAX_AGE * 1000
}

/**
 * Removes a transaction cookie from the browser by name.
 *
 * @param event - The request being answered.
 * @param name - The cookie name.
 */
const clearCookie = (event: H3Event, name: string): void => {
  deleteCookie(event, name, cookieOptions(0))
}

/**
 * Lists the transaction cookies the request carries, each opened.
 *
 * @param event - The request.
 * @returns One entry per cookie that has a transaction cookie name.
 */
const presentTransactions = (event: H3Event): PresentTransaction[] => {
  return Object.entries(parseCookies(event))
    .filter(([name]) => isTransactionCookieName(name))
    .map(([name, sealed]) => ({ name, transaction: readTransaction(name, sealed) }))
}

/**
 * Starts a transaction: seals it into its own cookie.
 *
 * First tidies the browser's other attempts: unreadable and expired ones are dropped, and when
 * `SSO_MAX_LIVE_TRANSACTIONS` live ones remain, the oldest are dropped until there is room, so the
 * cookies a browser sends back stay bounded. A newer attempt never replaces a live one that is
 * within the limit.
 *
 * @param event - The request being answered.
 * @param input - The attempt to remember.
 */
export const createTransaction = (event: H3Event, input: NewSignonTransaction): void => {
  const live: { name: string, createdAt: number }[] = []
  for (const { name, transaction } of presentTransactions(event)) {
    if (transaction !== null && isLive(transaction)) {
      live.push({ name, createdAt: transaction.createdAt })
    } else {
      clearCookie(event, name)
    }
  }
  live.sort((a, b) => a.createdAt - b.createdAt)
  while (live.length >= SSO_MAX_LIVE_TRANSACTIONS) {
    const oldest = live.shift()
    if (oldest) {
      clearCookie(event, oldest.name)
    }
  }

  const transaction: SignonTransaction = { v: TRANSACTION_VERSION, ...input, createdAt: Date.now() }
  const name = transactionCookieName(input.state)
  setCookie(event, name, sealText(JSON.stringify(transaction), transactionKey(), name), cookieOptions(SSO_FLOW_COOKIE_MAX_AGE))
}

/**
 * Finds the transaction for the `state` the provider returned.
 *
 * Only the cookie named after that `state` is read. It must authenticate, hold the same `state`
 * (compared in constant time) and be within the lifetime of an attempt. Nothing is deleted here.
 *
 * @param event - The callback request.
 * @param returnedState - The `state` from the callback query, if any.
 * @returns The transaction, or why there is none.
 */
export const openTransaction = (event: H3Event, returnedState: string | undefined): SignonTransactionLookup => {
  if (!returnedState) {
    return { status: SignonTransactionStatus.Missing, transaction: null }
  }
  const name = transactionCookieName(returnedState)
  const sealed = getCookie(event, name)
  if (!sealed) {
    return { status: SignonTransactionStatus.Missing, transaction: null }
  }
  const transaction = readTransaction(name, sealed)
  if (transaction === null || !sameText(transaction.state, returnedState)) {
    return { status: SignonTransactionStatus.Tampered, transaction: null }
  }
  if (!isLive(transaction)) {
    return { status: SignonTransactionStatus.Expired, transaction: null }
  }
  return { status: SignonTransactionStatus.Found, transaction }
}

/**
 * Deletes the transaction cookie of a `state`: the single use of a transaction.
 *
 * @param event - The request being answered.
 * @param state - The `state` of the attempt that was consumed.
 */
export const deleteTransaction = (event: H3Event, state: string): void => {
  clearCookie(event, transactionCookieName(state))
}

/**
 * Whether this browser has an interactive sign-in under way. The silent probe checks this so it
 * never starts an attempt while a person is still completing one.
 *
 * @param event - The request.
 * @returns True when a live interactive transaction cookie is present.
 */
export const hasPendingInteractiveTransaction = (event: H3Event): boolean => {
  return presentTransactions(event).some(({ transaction }) => {
    return transaction !== null && transaction.kind === SignonTransactionKind.Interactive && isLive(transaction)
  })
}
