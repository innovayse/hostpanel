/**
 * Marks the signed-in identity as already read and a customer, so a store that waits for the client
 * store's answer (`isCustomerAccount`) sends its request at once and a test counts only its own traffic.
 *
 * @module tests/helpers/knownCustomer
 */

import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { useClientStore } from '~/stores/client'

/**
 * Makes a Pinia active whose client store already holds a customer, for a test that mounts with it.
 *
 * @returns The Pinia, to pass to the mount.
 */
export const knownCustomerPinia = (): Pinia => {
  const pinia = createPinia()
  setActivePinia(pinia)
  markCustomerKnown()
  return pinia
}

/**
 * Marks the identity of the active Pinia as read and a customer.
 *
 * @returns Nothing.
 */
export const markCustomerKnown = (): void => {
  const client = useClientStore()
  client.user = makeClientUser()
  client.userLoaded = true
}
