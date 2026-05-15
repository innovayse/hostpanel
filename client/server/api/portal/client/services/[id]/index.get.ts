/**
 * GET /api/portal/client/services/:id
 * Returns details for a single service.
 * The C# backend only has a list endpoint, so we fetch all and filter.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  const services = await internalApiCall<Record<string, unknown>[]>(event, '/me/services')
  const s = services.find((svc) => String(svc.id) === id)

  if (!s) {
    throw createError({ statusCode: 404, statusMessage: 'Service not found' })
  }

  return {
    id: s.id,
    clientid: 0,
    pid: s.productId,
    regdate: '',
    name: s.productName,
    translated_name: s.productName,
    groupname: '',
    domain: s.domain ?? '',
    dedicatedip: '',
    serverid: 0,
    servername: '',
    serverip: '',
    serverhostname: '',
    firstpaymentamount: '0.00',
    recurringamount: s.price ?? '0.00',
    paymentmethod: '',
    paymentmethodname: '',
    billingcycle: s.billingCycle ?? '',
    nextduedate: s.nextRenewalAt ?? '',
    status: s.status ?? 'Active',
    username: s.username ?? 'provisioned',
    password: '',
    subscriptionid: '',
    promoid: 0,
    overideautosuspend: 0,
    overidesuspenduntil: '',
    ns1: '', ns2: '',
    assignedips: '',
    notes: '',
    diskusage: 0, disklimit: 0,
    bwusage: 0, bwlimit: 0,
    lastupdate: '',
    customfields: { customfield: [] },
    configoptions: { configoption: [] },
  }
})
