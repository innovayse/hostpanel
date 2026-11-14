/**
 * Turns the published-state filter of a list screen into what the API takes.
 */
import { PublishedFilter } from '@/types/publishedfilter'

/**
 * Reads the published filter as the API's `isPublished` switch.
 *
 * @param filter - The choice read from the address, undefined when none is set.
 * @returns True for the published rows, false for the drafts, undefined for no narrowing.
 */
export const isPublishedOfFilter = (filter: PublishedFilter | undefined): boolean | undefined =>
  filter === undefined ? undefined : filter === PublishedFilter.Published
