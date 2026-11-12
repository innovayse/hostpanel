/**
 * Colours of the domain availability states, shared by the search card and the results list.
 *
 * `unknown` is muted rather than red: nothing is wrong with the name, the check itself did not
 * complete. Each value is a complete class name so Tailwind keeps it.
 *
 * @module utils/domainStatus
 */

import { DomainResultStatus } from '~/types/domainresultstatus'
import { UiTone } from '~/utils/ui/enums'

/** Status dot colour per availability state. */
export const AVAILABILITY_DOT_CLASSES: Readonly<Record<DomainResultStatus, string>> = {
  [DomainResultStatus.Available]: 'bg-ok',
  [DomainResultStatus.Taken]: 'bg-danger',
  [DomainResultStatus.Unknown]: 'bg-mut2',
}

/** Status label colour per availability state. */
export const AVAILABILITY_LABEL_CLASSES: Readonly<Record<DomainResultStatus, string>> = {
  [DomainResultStatus.Available]: 'text-ok',
  [DomainResultStatus.Taken]: 'text-danger',
  [DomainResultStatus.Unknown]: 'text-mut2',
}

/** Badge tone per availability state, for the status chip of the results list. */
export const AVAILABILITY_TONES: Readonly<Record<DomainResultStatus, UiTone>> = {
  [DomainResultStatus.Available]: UiTone.Success,
  [DomainResultStatus.Taken]: UiTone.Danger,
  [DomainResultStatus.Unknown]: UiTone.Default,
}
