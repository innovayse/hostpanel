/**
 * Public WHMCS data composable
 * All calls go through useApi / apiFetch for automatic locale headers.
 */
export function useWhmcs() {
  async function checkDomain(domain: string) {
    return apiFetch('/api/portal/public/domain-check', {
      method: 'POST',
      body: { domain }
    })
  }

  const {
    data: products,
    pending: productsPending,
    error: productsError,
    refresh: refreshProducts
  } = useApi('/api/portal/public/products', {
    default: () => []
  })

  const {
    data: tldPricing,
    pending: tldPending
  } = useApi('/api/portal/public/tld-pricing')

  return {
    products,
    productsPending,
    productsError,
    refreshProducts,
    tldPricing,
    tldPending,
    checkDomain
  }
}
