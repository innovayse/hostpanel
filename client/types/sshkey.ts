/** One SSH key of a hosting service. Never carries key material beyond the fingerprint. */
import type { SshKeySyncState } from '~/types/sshkeysyncstate'

/** One SSH key of a service. */
export interface SshKey {
  /** Stable public id. */
  keyId: string
  /** Friendly label. */
  label: string
  /** `SHA256:` fingerprint. */
  fingerprint: string
  /** The addresses and CIDR networks the key may be used from; empty for a key that works from anywhere. */
  allowedFrom: string[]
  /** ISO creation timestamp. */
  createdAt: string
  /** Last state confirmed by the server. */
  syncState: SshKeySyncState
  /** True when the service has since moved server; support removes such keys. */
  isStale: boolean
  /** Whether the server reported holding the key, or null when not asked. */
  presentOnServer: boolean | null
}
