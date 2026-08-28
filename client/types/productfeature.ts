/** One specification line for a product, as `GET /api/portal/public/product-features` returns it. */
export interface ProductFeature {
  /** Line primary key. */
  id: number
  /** FK to the product the line describes. */
  productId: number
  /** What the line is called, e.g. "Disk space" — also what aligns comparison-table rows. */
  label: string
  /** What the line says for this product, e.g. "50 GB". */
  value: string
  /** Display order within the product. */
  sortOrder: number
}
