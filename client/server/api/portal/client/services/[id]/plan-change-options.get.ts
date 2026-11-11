/**
 * GET /api/portal/client/services/:id/plan-change-options
 * Lists the plans a hosting service can move to, with the prorated amount of each, via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  return await internalApiCall<Record<string, unknown>[]>(event, `/me/services/${id}/plan-change-options`)
})
