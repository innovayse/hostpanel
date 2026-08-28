import { describe, expect, it } from 'vitest'
import { buildComparisonRows } from './useProductFeatures'
import type { ProductFeature } from '~/types/productfeature'

/**
 * Builds a specification line.
 *
 * @param productId Product the line belongs to.
 * @param label Feature name.
 * @param value Value for this product.
 * @param sortOrder Position within the product.
 */
const line = (productId: number, label: string, value: string, sortOrder: number): ProductFeature =>
  ({ id: productId * 100 + sortOrder, productId, label, value, sortOrder })

describe('buildComparisonRows', () => {
  it('has nothing to compare without plans', () => {
    expect(buildComparisonRows([line(1, 'Disk', '10 GB', 0)], [])).toEqual([])
  })

  it('has nothing to compare when no plan specifies anything', () => {
    expect(buildComparisonRows([], [1, 2])).toEqual([])
  })

  it('aligns each plan value to its column', () => {
    const features = [
      line(1, 'Disk', '10 GB', 0),
      line(1, 'Bandwidth', '1 TB', 1),
      line(2, 'Disk', '50 GB', 0),
      line(2, 'Bandwidth', '10 TB', 1),
    ]

    expect(buildComparisonRows(features, [1, 2])).toEqual([
      { label: 'Disk', values: ['10 GB', '50 GB'] },
      { label: 'Bandwidth', values: ['1 TB', '10 TB'] },
    ])
  })

  it('follows the column order it is given, not the order of the input', () => {
    const features = [line(2, 'Disk', '50 GB', 0), line(1, 'Disk', '10 GB', 0)]

    expect(buildComparisonRows(features, [2, 1])).toEqual([
      { label: 'Disk', values: ['50 GB', '10 GB'] },
    ])
  })

  it('leaves a gap rather than shifting a column when a plan omits a line', () => {
    const features = [
      line(1, 'Disk', '10 GB', 0),
      line(2, 'Disk', '50 GB', 0),
      line(2, 'Backups', 'Daily', 1),
    ]

    expect(buildComparisonRows(features, [1, 2])).toEqual([
      { label: 'Disk', values: ['10 GB', '50 GB'] },
      { label: 'Backups', values: ['', 'Daily'] },
    ])
  })

  it('keeps a row a single plan uses, so nothing an operator entered is dropped', () => {
    const features = [line(2, 'Dedicated IP', 'Included', 0)]

    expect(buildComparisonRows(features, [1, 2])).toEqual([
      { label: 'Dedicated IP', values: ['', 'Included'] },
    ])
  })

  it('orders rows by sort order within a plan, regardless of input order', () => {
    const features = [
      line(1, 'Bandwidth', '1 TB', 2),
      line(1, 'Disk', '10 GB', 1),
      line(1, 'Databases', '5', 3),
    ]

    expect(buildComparisonRows(features, [1]).map(r => r.label))
      .toEqual(['Disk', 'Bandwidth', 'Databases'])
  })
})
