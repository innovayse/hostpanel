/**
 * POST /api/portal/public/domain-check
 * Checks domain availability via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const domain = body?.domain?.trim()

  if (!domain) {
    throw createError({ statusCode: 400, statusMessage: serverT(event, 'error.domainRequired') })
  }

  return await internalApiCall<Record<string, unknown>>(event, '/domains/check', {
    method: 'POST',
    body: { domain },
  })
})
