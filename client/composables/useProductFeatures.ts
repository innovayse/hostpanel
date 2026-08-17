/** One specification line as the API returns it. */
export interface ProductFeature {
  id: number
  productId: number
  label: string
  value: string
  sortOrder: number
}

/** One row of the comparison table: a label and its value per plan, in plan order. */
export interface ComparisonRow {
  label: string
  /** Values aligned to the plan ids passed in; an empty string where a plan has no such line. */
  values: string[]
}

/**
 * Turns per-product specification lines into aligned comparison rows.
 *
 * Rows are keyed by label, which is what makes the table line up: products are
 * walked in display order and each new label is appended the first time it is
 * seen, so the leftmost plan sets the running order and later plans contribute
 * only the lines it does not already have. A plan missing a line gets an empty
 * value rather than shifting the column, and a label only one plan uses still
 * gets its own row.
 *
 * @param features Specification lines for any subset of the products.
 * @param productIds Products to compare, in the order their columns appear.
 * @returns Rows in display order; empty when nothing is specified.
 */
export const buildComparisonRows = (
  features: ProductFeature[],
  productIds: number[],
): ComparisonRow[] => {
  if (productIds.length === 0) return []

  const byProduct = new Map<number, ProductFeature[]>()
  for (const feature of features) {
    const list = byProduct.get(feature.productId)
    if (list) list.push(feature)
    else byProduct.set(feature.productId, [feature])
  }

  const order: string[] = []
  const seen = new Set<string>()
  for (const id of productIds) {
    const lines = (byProduct.get(id) ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder)
    for (const line of lines) {
      if (!seen.has(line.label)) {
        seen.add(line.label)
        order.push(line.label)
      }
    }
  }

  return order.map(label => ({
    label,
    values: productIds.map(id =>
      byProduct.get(id)?.find(line => line.label === label)?.value ?? ''),
  }))
}

/**
 * Loads the specification lines for a product group.
 *
 * @param groupId Product group whose plans are being compared.
 * @returns The raw lines and the request's pending state.
 */
export const useProductFeatures = (groupId: number) => {
  const { data, pending } = useFetch<ProductFeature[]>('/api/portal/public/product-features', {
    query: { gid: groupId },
  })

  const features = computed(() => data.value ?? [])

  return { features, pending }
}
