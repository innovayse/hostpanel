/**
 * Helpers for a screen that draws its form from a skeleton schema but shows only some of the schema's fields.
 *
 * The schema lists every field the screen can have, because a skeleton cannot know the mode of the form it stands in for.
 * A screen whose mode hides a field (an edit only status, a link that a linked product replaces) takes the schema's
 * columns through here, so the card of the field is not drawn empty.
 */
import type { SkeletonSection } from '@/utils/ui/skeletonSchema'

/**
 * Removes fields from the sections of a form schema.
 *
 * @param columns - The `columns` of a form schema.
 * @param keys - Keys of the fields to leave out.
 * @returns The same columns without those fields; the input is not changed.
 */
export const withoutFields = (
  columns: readonly (readonly SkeletonSection[])[],
  keys: readonly string[],
): readonly (readonly SkeletonSection[])[] =>
  columns.map((column) => column.map((section) => ({ ...section, fields: section.fields.filter((field) => !keys.includes(field.key)) })))
