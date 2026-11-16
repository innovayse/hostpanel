/**
 * Helpers for reading the address query, shared by every composable that keeps screen state in it.
 */
import type { LocationQueryValue } from 'vue-router'

/**
 * Reads a query value as one string.
 *
 * @param value - What the router holds for a key: nothing, one value, or a repeated key.
 * @returns The first value, or an empty string when the key is missing.
 */
export const readQueryString = (value: LocationQueryValue | LocationQueryValue[] | undefined): string =>
  (Array.isArray(value) ? value[0] : value) ?? ''
