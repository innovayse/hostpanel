/**
 * GET /api/portal/client/services/:id/hosting-info
 * Returns real-time nameserver and SSL certificate info for the service's domain.
 * Does DNS NS lookup + TLS handshake to get live data.
 */
import dns from 'node:dns/promises'
import tls from 'node:tls'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  const service = await internalApiCall<{ domain?: string }>(event, `/me/services/${id}`)

  const domain = service?.domain
  if (!domain) {
    return { nameservers: [], ssl: null }
  }

  // Run DNS and SSL checks in parallel
  const [nameservers, ssl] = await Promise.all([
    resolveNameservers(domain),
    checkSsl(domain),
  ])

  return { nameservers, ssl }
})

/**
 * Resolves nameservers for a given domain.
 *
 * @param domain - Domain name to look up
 * @returns Sorted list of nameserver hostnames, empty on failure
 */
async function resolveNameservers(domain: string): Promise<string[]> {
  try {
    const ns = await dns.resolveNs(domain)
    return ns.sort()
  } catch {
    return []
  }
}

/**
 * Checks TLS certificate validity for a domain on port 443.
 *
 * @param domain - Domain name to connect to
 * @returns Certificate info object, or null on failure
 */
async function checkSsl(domain: string): Promise<{
  valid: boolean
  issuer: string
  startDate: string
  expiryDate: string
} | null> {
  return new Promise((resolve) => {
    const socket = tls.connect(
      { host: domain, port: 443, servername: domain, timeout: 5000 },
      () => {
        const cert = socket.getPeerCertificate()
        socket.destroy()

        if (!cert || !cert.valid_from) {
          resolve(null)
          return
        }

        const validFrom = new Date(cert.valid_from)
        const validTo = new Date(cert.valid_to)
        const now = new Date()
        const valid = now >= validFrom && now <= validTo && !socket.authorizationError

        resolve({
          valid,
          issuer: cert.issuer?.CN || cert.issuer?.O || '',
          startDate: formatDate(validFrom),
          expiryDate: formatDate(validTo),
        })
      },
    )

    socket.on('error', () => {
      socket.destroy()
      resolve(null)
    })

    socket.setTimeout(5000, () => {
      socket.destroy()
      resolve(null)
    })
  })
}

/**
 * Formats a Date as DD/MM/YYYY string.
 *
 * @param d - Date to format
 * @returns Formatted date string
 */
function formatDate(d: Date): string {
  const day = String(d.getDate()).padStart(2, '0')
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const year = d.getFullYear()
  return `${day}/${month}/${year}`
}
