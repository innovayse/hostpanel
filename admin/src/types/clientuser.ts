/**
 * A sub-account on a client, the permission flags it can hold, and their labels.
 * 
 * The labels sit beside the enum rather than in a component so every screen that renders a
 * permission spells it the same way.
 */

import type { Domain } from './domain'
import type { Product } from './product'

/** User linked to a client account with permissions. */
export interface ClientUserItem {
  /** Identity user ID. */
  userId: string
  /** First name. */
  firstName: string
  /** Last name. */
  lastName: string
  /** Email address. */
  email: string
  /** True if this user is the account owner. */
  isOwner: boolean
  /** Granted permissions as bit-flags integer. */
  permissions: number
  /** Last login timestamp or null. */
  lastLoginAt: string | null
  /** When the user was linked. */
  createdAt: string
}

/** Granular permissions for users linked to a client account. */
export enum ClientPermission {
  /** No permissions. */
  None = 0,
  /** Modify the master account profile. */
  ModifyMasterProfile = 1,
  /** View and manage contacts. */
  ViewManageContacts = 2,
  /** View products and services. */
  ViewProductsServices = 4,
  /** View and modify product passwords. */
  ViewModifyPasswords = 8,
  /** Allow single sign-on to hosting panels. */
  AllowSingleSignOn = 16,
  /** View domains. */
  ViewDomains = 32,
  /** Manage domain settings. */
  ManageDomainSettings = 64,
  /** View and pay invoices. */
  ViewPayInvoices = 128,
  /** View and accept quotes. */
  ViewAcceptQuotes = 256,
  /** View and open support tickets. */
  ViewOpenSupportTickets = 512,
  /** View and manage affiliate account. */
  ViewManageAffiliate = 1024,
  /** View emails. */
  ViewEmails = 2048,
  /** Place new orders, upgrades, and cancellations. */
  PlaceNewOrders = 4096,
  /** All permissions combined. */
  All = 8191,
}

/** Labels for each permission flag, used for checkbox rendering. */
export const PERMISSION_LABELS: { flag: ClientPermission; label: string }[] = [
  { flag: ClientPermission.ModifyMasterProfile, label: 'Modify Master Account Profile' },
  { flag: ClientPermission.ViewManageContacts, label: 'View & Manage Contacts' },
  { flag: ClientPermission.ViewProductsServices, label: 'View Products & Services' },
  { flag: ClientPermission.ViewModifyPasswords, label: 'View & Modify Product Passwords' },
  { flag: ClientPermission.AllowSingleSignOn, label: 'Allow Single Sign-On' },
  { flag: ClientPermission.ViewDomains, label: 'View Domains' },
  { flag: ClientPermission.ManageDomainSettings, label: 'Manage Domain Settings' },
  { flag: ClientPermission.ViewPayInvoices, label: 'View & Pay Invoices' },
  { flag: ClientPermission.ViewAcceptQuotes, label: 'View & Accept Quotes' },
  { flag: ClientPermission.ViewOpenSupportTickets, label: 'View & Open Support Tickets' },
  { flag: ClientPermission.ViewManageAffiliate, label: 'View & Manage Affiliate Account' },
  { flag: ClientPermission.ViewEmails, label: 'View Emails' },
  { flag: ClientPermission.PlaceNewOrders, label: 'Place New Orders/Upgrades/Cancellations' },
]
