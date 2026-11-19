/**
 * Layout vocabulary of the skeleton kit (`UiSkeletonOf`, `UiSchemaForm`, `UiFormSection`, `UiDetailCards`, `UiStatRow`):
 * how many grid columns a count of side by side stacks takes, and the placeholder of each field kind.
 */
import { SkeletonFieldKind } from '@/utils/ui/skeletonSchema'
import type { SkeletonField } from '@/utils/ui/skeletonSchema'
import type { UiGridColumns } from '@/utils/ui/layout'

/**
 * The grid column count for a number of stacks standing side by side; one stack is one column, never a two column grid.
 *
 * @param count - How many stacks the schema lists.
 * @returns One to four columns; a list longer than four still takes four.
 */
export const gridColumnsOf = (count: number): UiGridColumns => {
  if (count <= 1) {
    return 1
  }
  if (count === 2) {
    return 2
  }
  return count === 3 ? 3 : 4
}

/** The field kind a single bar stands in for (an input is the `UiField` skeleton itself, a radio group draws its options). */
export type SkeletonBarFieldKind = Exclude<SkeletonFieldKind, typeof SkeletonFieldKind.Input | typeof SkeletonFieldKind.Radio>

/** Placeholder classes of the control kinds `UiField` does not draw itself, literal so Tailwind emits them. */
export const fieldBarClasses: Record<SkeletonBarFieldKind, string> = {
  [SkeletonFieldKind.Textarea]: 'h-24 w-full rounded-control',
  [SkeletonFieldKind.Toggle]: 'h-6 w-11 shrink-0 rounded-full',
  [SkeletonFieldKind.Check]: 'h-5 w-48 rounded-md',
}

/**
 * The placeholder classes of a field that has a bar of its own.
 *
 * @param field - The field of a schema.
 * @returns The classes of its bar, or `undefined` for a single line input, which `UiField` draws itself, and for a radio group, which draws its options.
 */
export const fieldBarClassOf = (field: SkeletonField): string | undefined => {
  if (field.kind === undefined || field.kind === SkeletonFieldKind.Input || field.kind === SkeletonFieldKind.Radio) {
    return undefined
  }
  return fieldBarClasses[field.kind]
}

/**
 * Whether a field draws its own placeholder instead of the `UiField` input skeleton.
 *
 * @param field - The field of a schema.
 * @returns True for a textarea, a toggle, a check and a radio group.
 */
export const ownsFieldBody = (field: SkeletonField): boolean => field.kind !== undefined && field.kind !== SkeletonFieldKind.Input
