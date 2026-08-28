import type { ClientUser } from '~/types/clientuser'
import type { TwoFactorChallenge } from '~/types/twofactorchallenge'

/**
 * The two things a password sign-in can answer with: a signed-in account, or the second-factor
 * challenge standing between the caller and one. Narrow with `'twoFactorRequired' in result`.
 */
export type LoginResult = ClientUser | TwoFactorChallenge
