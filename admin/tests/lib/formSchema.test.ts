/**
 * Tests for the form schema helper: fields are left out of every section and the input stays as it was.
 */
import { describe, expect, it } from 'vitest'
import { withoutFields } from '@/lib/formSchema'
import type { SkeletonSection } from '@/utils/ui/skeletonSchema'

/** Two columns of sections the tests cut fields from. */
const columns: readonly (readonly SkeletonSection[])[] = [
  [{ key: 'a', fields: [{ key: 'one' }, { key: 'two' }] }],
  [{ key: 'b', fields: [{ key: 'two' }, { key: 'three' }] }],
]

describe('withoutFields', () => {
  it('removes the named fields from every section and keeps the rest in order', () => {
    const result = withoutFields(columns, ['two'])

    expect(result[0]?.[0]?.fields.map((field) => field.key)).toEqual(['one'])
    expect(result[1]?.[0]?.fields.map((field) => field.key)).toEqual(['three'])
  })

  it('does not change its input', () => {
    withoutFields(columns, ['one', 'two', 'three'])

    expect(columns[0]?.[0]?.fields).toHaveLength(2)
  })
})
