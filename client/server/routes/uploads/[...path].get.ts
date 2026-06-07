/**
 * GET /uploads/**
 *
 * Proxies uploaded file requests to the backend API server.
 * Slide images are stored on the API server's wwwroot/uploads/ directory
 * and need to be accessible from the client portal's domain.
 */
export default defineEventHandler(async (event) => {
  const path = getRouterParam(event, 'path')
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'

  return proxyRequest(event, `${apiUrl}/uploads/${path}`)
})
