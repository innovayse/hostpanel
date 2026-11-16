/**
 * Turns the enabled-state filter of a list screen into what the API takes.
 */
import { EnabledFilter } from '@/types/enabledfilter'

/**
 * Reads the enabled filter as the API's `enabled` switch.
 *
 * @param filter - The choice read from the address, undefined when none is set.
 * @returns True for the switched-on rows, false for the switched-off ones, undefined for no narrowing.
 */
export const enabledOfFilter = (filter: EnabledFilter | undefined): boolean | undefined =>
  filter === undefined ? undefined : filter === EnabledFilter.Enabled
