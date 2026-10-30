/**
 * Encryption of the portal sessions at rest, and the AES-256-GCM seal the sign-in transaction shares.
 *
 * A session holds the person's access and refresh tokens. In Redis they are kept as AES-256-GCM
 * ciphertext under a key from the runtime config (`sessionEncryptionKey`, `NUXT_SESSION_ENCRYPTION_KEY`),
 * so a reader of the Redis data (a backup, a stray client, a shared instance) cannot use them.
 * The stored form is `v1.<base64url(iv | tag | ciphertext)>`, with a fresh random IV per write.
 *
 * @module server/utils/session-crypto
 */

import { createCipheriv, createDecipheriv, hkdfSync, randomBytes } from 'node:crypto'

/** Length of the key in bytes (AES-256). */
const KEY_BYTES = 32

/** Length of the GCM initialisation vector in bytes. */
const IV_BYTES = 12

/** Length of the GCM authentication tag in bytes. */
const TAG_BYTES = 16

/** The cipher in use. */
const ALGORITHM = 'aes-256-gcm'

/** Marks the stored form, so a later change of scheme can be told apart. */
const VERSION_PREFIX = 'v1.'

/**
 * Reads the session encryption key from its configured form.
 *
 * @param raw - The configured key: 32 random bytes, base64-encoded; empty when none is set.
 * @returns The key, or null when none is configured and the process runs in development.
 * @throws Error when the key is missing outside development, or is not 32 bytes of base64.
 */
export const readSessionKey = (raw: unknown): Buffer | null => {
  const text = typeof raw === 'string' ? raw.trim() : ''
  if (!text) {
    if (process.env.NODE_ENV === 'development') {
      return null
    }
    throw new Error('The session encryption key (NUXT_SESSION_ENCRYPTION_KEY) is required when sessions are kept in Redis.')
  }
  const key = Buffer.from(text, 'base64')
  if (key.length !== KEY_BYTES) {
    throw new Error('The session encryption key must be 32 random bytes, base64-encoded.')
  }
  return key
}


/**
 * Derives a key for one purpose from the configured key (HKDF-SHA256), so two uses of the same
 * secret never share a key.
 *
 * @param key - The 32-byte configured key.
 * @param purpose - A fixed label naming the use, for example the sign-in transaction.
 * @returns A 32-byte key that is unrelated to the configured one and to other purposes.
 */
export const deriveKey = (key: Buffer, purpose: string): Buffer => {
  return Buffer.from(hkdfSync('sha256', key, Buffer.alloc(0), purpose, KEY_BYTES))
}

/**
 * Seals a text under a key, binding it to additional authenticated data.
 *
 * The one AES-256-GCM implementation of this app: the stored session and the sign-in transaction
 * cookie both use it. The additional data is not stored and not secret, but it is part of the
 * authentication tag, so a sealed value only opens under the same data it was sealed with (a cookie
 * moved to another cookie name, for example, no longer opens).
 *
 * @param plain - The text to seal.
 * @param key - The 32-byte key.
 * @param aad - The additional authenticated data; empty for none.
 * @returns The stored form `v1.<base64url(iv | tag | ciphertext)>`, with a fresh random IV.
 */
export const sealText = (plain: string, key: Buffer, aad: string = ''): string => {
  const iv = randomBytes(IV_BYTES)
  const cipher = createCipheriv(ALGORITHM, key, iv)
  cipher.setAAD(Buffer.from(aad, 'utf8'))
  const sealed = Buffer.concat([cipher.update(plain, 'utf8'), cipher.final()])
  return `${VERSION_PREFIX}${Buffer.concat([iv, cipher.getAuthTag(), sealed]).toString('base64url')}`
}

/**
 * Opens a text sealed with `sealText`.
 *
 * @param stored - The stored form.
 * @param key - The 32-byte key.
 * @param aad - The additional authenticated data the text was sealed with; empty for none.
 * @returns The text, or null when the value is not in the stored form, was tampered with, was
 * written under another key or under other additional data.
 */
export const openText = (stored: string, key: Buffer, aad: string = ''): string | null => {
  if (!stored.startsWith(VERSION_PREFIX)) {
    return null
  }
  const bytes = Buffer.from(stored.slice(VERSION_PREFIX.length), 'base64url')
  if (bytes.length <= IV_BYTES + TAG_BYTES) {
    return null
  }
  try {
    const decipher = createDecipheriv(ALGORITHM, key, bytes.subarray(0, IV_BYTES))
    decipher.setAAD(Buffer.from(aad, 'utf8'))
    decipher.setAuthTag(bytes.subarray(IV_BYTES, IV_BYTES + TAG_BYTES))
    return Buffer.concat([decipher.update(bytes.subarray(IV_BYTES + TAG_BYTES)), decipher.final()]).toString('utf8')
  } catch {
    return null
  }
}

/**
 * Encrypts a serialised session.
 *
 * @param plain - The session JSON.
 * @param key - The 32-byte key.
 * @returns The stored form.
 */
export const encryptSession = (plain: string, key: Buffer): string => sealText(plain, key)

/**
 * Decrypts a stored session.
 *
 * @param stored - The stored form from `encryptSession`.
 * @param key - The 32-byte key.
 * @returns The session JSON, or null when the value is not in the stored form, was tampered with,
 * or was written under another key.
 */
export const decryptSession = (stored: string, key: Buffer): string | null => openText(stored, key)
