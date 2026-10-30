/**
 * The one bit test behind every "may this person do that" decision in the client portal.
 *
 * {@link mayPerform} is the decision itself: it adds the two cases where there is no permission to
 * hold, so every caller (the client store's `mayDo`, which the sections and the checkout ask) answers alike.
 *
 * The permission value is the integer of `ClientPermission` flags the API reports for the signed-in
 * person (`clientPermissions` on `GET /api/auth/me`, carried on the portal's `ClientUser`). It only
 * decides what to show; the API enforces the same flags on every portal request, so a hidden link is
 * a courtesy and never the protection.
 *
 * @module utils/holdsPermission
 */

import type { ClientUser } from '~/types/clientuser'

/**
 * Whether a permission value contains every bit of the required flag.
 *
 * @param held - The permission integer the person holds; a missing value holds nothing.
 * @param required - One flag, or several combined with bitwise OR (all of them are required).
 *        `0` names no requirement and is always satisfied.
 * @returns True when every required bit is set in `held`.
 */
export const holdsPermission = (held: number | null | undefined, required: number): boolean => {
  if (required === 0) {
    return true
  }
  return ((held ?? 0) & required) === required
}

/**
 * Whether the signed-in person may do something that needs a permission flag.
 *
 * Permissions are granted on a client account, so they only restrict a person who acts for one.
 * A signed-in first-time buyer has no account yet and holds no flags, yet the API lets them order
 * (it creates their account); refusing them here would block the very first purchase. The API
 * still enforces the flags on every request, so this only decides what to offer.
 *
 * @param person - The signed-in person, or null while none is loaded (nothing to hide behind, so
 *        the section still asks and the API decides).
 * @param required - One flag, or several combined with bitwise OR (all of them are required).
 * @returns False only for a person who acts for an account and does not hold every required flag.
 */
export const mayPerform = (
  person: Pick<ClientUser, 'permissions' | 'actsForAccount'> | null | undefined,
  required: number
): boolean => {
  // `=== false`: a record that does not say (an older answer) counts as acting for an account,
  // the side that shows less.
  return !person || person.actsForAccount === false || holdsPermission(person.permissions, required)
}
