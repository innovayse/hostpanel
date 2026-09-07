import type { Product } from './product'

/** Represents a system setting key-value pair. */
export interface Setting {
  /** Unique setting identifier. */
  id: number
  /** Setting key. */
  key: string
  /** Setting value. */
  value: string
  /** Optional human-readable description. */
  description?: string
}
