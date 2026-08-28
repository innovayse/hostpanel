/** One hosting service the client owns, as `GET /api/portal/client/services` returns it. */
export interface ClientService {
  /** Service primary key. */
  id: number
  /** FK to the owning client. */
  clientid: number
  /** FK to the product this service was ordered from. */
  pid: number
  /** Registration date of the service. */
  regdate: string
  /** Product name as configured. */
  name: string
  /** Product name in the requested locale, when the backend has one. */
  translated_name?: string
  /** Product group the product belongs to. */
  groupname: string
  /** Domain the service is attached to. */
  domain: string
  /** Dedicated IP assigned to the service, empty when shared. */
  dedicatedip: string
  /** FK to the server hosting the service. */
  serverid: number
  /** Human-readable server name. */
  servername: string
  /** Server IP address. */
  serverip: string
  /** Server hostname. */
  serverhostname: string
  /** Why the service is suspended, empty when it is not. */
  suspensionreason: string
  /** Amount charged on the first payment, as a formatted decimal string. */
  firstpaymentamount: string
  /** Amount charged each cycle thereafter, as a formatted decimal string. */
  recurringamount: string
  /** Payment gateway module name. */
  paymentmethod: string
  /** Payment gateway display name. */
  paymentmethodname: string
  /** Billing cycle, e.g. "Monthly" or "Annually". */
  billingcycle: string
  /** Next renewal due date. */
  nextduedate: string
  /** Current lifecycle status, e.g. "Active" or "Suspended". */
  status: string
  /** Control-panel username for the service. */
  username: string
  /** Disk used, in the units the server reports. */
  diskusage: string
  /** Disk allowance, in the units the server reports. */
  disklimit: string
  /** Bandwidth used, in the units the server reports. */
  bwusage: string
  /** Bandwidth allowance, in the units the server reports. */
  bwlimit: string
  /** When the usage figures above were last refreshed. */
  lastupdate: string
}
