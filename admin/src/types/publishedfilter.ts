/**
 * The choices of a published-state filter on a list whose rows are published or kept as drafts (knowledge base articles,
 * announcements). "Any" is the empty value of the dropdown, so it is not a member.
 */

/** Which rows a list shows by their published state. */
export enum PublishedFilter {
  /** Only the rows visible to clients. */
  Published = 'published',
  /** Only the rows kept back as drafts. */
  Draft = 'draft',
}
