/**
 * What the account store answers when the payer asks to save a card.
 *
 * @module types/cardsaveresult
 */

import type { CardSaveOutcome } from '~/types/cardsaveoutcome'

/** The outcome of an add-card attempt and, for a hosted gateway, the next step. */
export interface CardSaveResult {
  /** How the attempt ended. */
  outcome: CardSaveOutcome
  /** Where to send the payer when {@link outcome} is `Redirect`; null otherwise. */
  redirectUrl: string | null
}
