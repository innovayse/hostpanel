/**
 * The DNS, nameserver and forwarding settings a client manages on a domain, as the API sends
 * them on every domain it returns to the client area.
 *
 * @module types/domainmanagement
 */

import type { DnsRecord } from '~/types/dnsrecord'
import type { DomainNameserver } from '~/types/domainnameserver'
import type { EmailForwardingRule } from '~/types/emailforwardingrule'

/** The optional operations the registrar that holds a domain offers; a screen offers an action only when its flag is on. */
export interface DomainCapabilities {
  /** Whether the registrar can switch DNS management on and off. */
  dnsManagement: boolean
  /** Whether the registrar can forward the domain's mail. */
  emailForwarding: boolean
  /** Whether the registrar answers a WHOIS lookup. */
  whois: boolean
}

/** The settings of a domain the client can manage. */
export interface DomainManagement {
  /** Whether DNS management is on. */
  dnsManagement: boolean
  /** Whether email forwarding is on. */
  emailForwarding: boolean
  /** The domain's nameservers, in the order the registrar holds them. */
  nameservers: DomainNameserver[]
  /** The domain's DNS records. */
  dnsRecords: DnsRecord[]
  /** The domain's email forwarding rules. */
  emailForwardingRules: EmailForwardingRule[]
  /** What the registrar that holds the domain offers. */
  capabilities: DomainCapabilities
}
