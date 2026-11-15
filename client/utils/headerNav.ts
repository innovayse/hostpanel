/**
 * Builds the classic header navigation from the catalogue.
 *
 * Pure functions: the header passes in the translator, the locale prefixer and what the catalogue
 * store holds, and gets back the entries to draw. Hosting is a dropdown of the catalogue's plan
 * groups (shared hosting, VPS, dedicated servers and the other service groups it sells, each with a
 * short description), and Support groups the help pages. The top level stays short on purpose: it
 * leaves room for the controls on the right of the bar.
 *
 * @module utils/headerNav
 */

import { HOSTING_GROUP_ID } from '~/constants/catalogGroup'
import { SUPPORT_LINK_ICON, SupportLinkKey } from '~/constants/headerNavIcon'
import { productTypeIcon } from '~/constants/productTypeIcon'
import {
  SITE_CONTACT_PATH,
  SITE_DOMAINS_PATH,
  SITE_FAQ_PATH,
  SITE_HOSTING_PATH,
  SITE_KNOWLEDGEBASE_PATH,
  SITE_PRODUCTS_PATH,
} from '~/constants/siteRoutes'
import type { HeaderNavChild } from '~/types/headernavchild'
import type { HeaderNavItem } from '~/types/headernavitem'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import { ProductType } from '~/types/producttype'
import { dominantProductType, familyConfig, isFamilyGroup, parseDescription } from '~/utils/catalogGroups'

/** Translates a message key. */
export type HeaderTranslate = (key: string) => string

/** Prefixes a route with the active locale. */
export type HeaderLocalePath = (path: string) => string

/** What {@link buildHeaderNav} reads. */
export interface HeaderNavInput {
  /** Translator for the active locale. */
  t: HeaderTranslate
  /** Locale prefixer. */
  localePath: HeaderLocalePath
  /** The catalogue's product groups; empty until loaded. */
  groups: readonly PortalProductGroup[]
  /** The products of the family groups; empty until loaded. */
  products: readonly PortalProduct[]
}

/** Short description line of a Hosting dropdown entry, by the service type its group sells. */
const HOSTING_MENU_DESC_KEYS: Readonly<Partial<Record<ProductType, string>>> = {
  [ProductType.SharedHosting]: 'nav.hostingDesc',
  [ProductType.Vps]: 'nav.vpsDesc',
  [ProductType.Dedicated]: 'nav.serversDesc',
  [ProductType.Email]: 'nav.emailDesc',
  [ProductType.Ssl]: 'nav.sslDesc',
  [ProductType.Backup]: 'nav.backupDesc',
}

/**
 * The description line under a Hosting dropdown entry.
 *
 * The group's own short description wins when the operator wrote one (its first paragraph, as
 * plain text); otherwise the line written for the service type the group sells, so the row never
 * just repeats its title.
 *
 * @param group - The catalogue group.
 * @param fallbackKey - Message key of the line for the group's service type.
 * @param t - Translator for the active locale.
 * @returns The description to show.
 */
export const hostingEntryDescription = (group: PortalProductGroup, fallbackKey: string, t: HeaderTranslate): string =>
  parseDescription(group.description ?? '').summary || t(fallbackKey)

/**
 * Whether a navigation target is the page being viewed: its own path or a page below it.
 *
 * @param path - The current route path.
 * @param href - The link target.
 * @returns True for the current target.
 */
export const isHeaderPathActive = (path: string, href: string | undefined): boolean => {
  if (!href) {
    return false
  }
  return path === href || path.startsWith(`${href}/`)
}

/**
 * The length of the longest target of an entry (its own or a member's) that matches the page, or
 * zero when none does. A longer match is a more specific one.
 *
 * @param path - The current route path.
 * @param item - The navigation entry.
 * @returns The match length, zero for no match.
 */
const headerMatchLength = (path: string, item: HeaderNavItem): number =>
  [item.to, ...(item.children ?? []).map(child => child.to)]
    .filter((href): href is string => isHeaderPathActive(path, href))
    .reduce((longest, href) => Math.max(longest, href.length), 0)

/**
 * The one top-level entry that stands for the page being viewed.
 *
 * Several entries can match a path (a Hosting dropdown member lives under `/products/<slug>`, and
 * Products matches the whole `/products` prefix), so the entry with the most specific target, the
 * longest matching one, wins. On a tie the earlier entry wins.
 *
 * @param path - The current route path.
 * @param items - The navigation entries in display order.
 * @returns The key of the active entry, or undefined when none matches.
 */
export const activeHeaderKey = (path: string, items: readonly HeaderNavItem[]): string | undefined =>
  items.reduce<{ key?: string, length: number }>((best, item) => {
    const length = headerMatchLength(path, item)
    return length > best.length ? { key: item.key, length } : best
  }, { length: 0 }).key

/**
 * Builds the header entries.
 *
 * @param input - Translator, locale prefixer and the catalogue state.
 * @returns The entries in display order: Hosting, Domains, Products, Support.
 */
export const buildHeaderNav = ({ t, localePath, groups, products }: HeaderNavInput): HeaderNavItem[] => {
  const families = groups.filter(isFamilyGroup)

  /**
   * The products a group holds.
   *
   * @param group - The catalogue group.
   * @returns Its products.
   */
  const productsOf = (group: PortalProductGroup): PortalProduct[] =>
    products.filter(product => product.groupId === group.id)

  /**
   * The service type a group sells.
   *
   * @param group - The catalogue group.
   * @returns The type, or null when unknown.
   */
  const typeOf = (group: PortalProductGroup): ProductType | null =>
    group.id === HOSTING_GROUP_ID ? ProductType.SharedHosting : dominantProductType(productsOf(group))

  /**
   * The localised page of a catalogue group.
   *
   * @param group - The catalogue group.
   * @returns The route of the group's family page.
   */
  const pageOf = (group: PortalProductGroup): string => localePath(familyConfig(group, productsOf(group)).learnMoreUrl)

  const hostingChildren: HeaderNavChild[] = [
    {
      key: 'hosting-all',
      label: t('nav.allHosting'),
      icon: productTypeIcon(ProductType.SharedHosting),
      desc: t('nav.hostingDesc'),
      to: localePath(SITE_HOSTING_PATH),
    },
    ...families
      .flatMap((group) => {
        const type = typeOf(group)
        const descKey = type ? HOSTING_MENU_DESC_KEYS[type] : undefined
        // A group of a type the menu has no sentence for, and the hosting group itself, get no entry.
        return group.id !== HOSTING_GROUP_ID && descKey
          ? [{
            key: `group-${group.id}`,
            label: group.name,
            icon: productTypeIcon(type),
            desc: hostingEntryDescription(group, descKey, t),
            to: pageOf(group),
          }]
          : []
      }),
  ]

  const items: HeaderNavItem[] = [
    hostingChildren.length > 1
      ? { key: 'hosting', label: t('nav.hosting'), children: hostingChildren }
      : { key: 'hosting', label: t('nav.hosting'), to: localePath(SITE_HOSTING_PATH) },
    { key: 'domains', label: t('nav.domains'), to: localePath(SITE_DOMAINS_PATH) },
  ]


  items.push(
    { key: 'products', label: t('nav.products'), to: localePath(SITE_PRODUCTS_PATH) },
    {
      key: 'support',
      label: t('nav.support'),
      children: [
        { key: SupportLinkKey.Help, label: t('nav.help'), icon: SUPPORT_LINK_ICON[SupportLinkKey.Help], desc: t('nav.helpDesc'), to: localePath(SITE_KNOWLEDGEBASE_PATH) },
        { key: SupportLinkKey.Faq, label: t('nav.faq'), icon: SUPPORT_LINK_ICON[SupportLinkKey.Faq], desc: t('nav.faqDesc'), to: localePath(SITE_FAQ_PATH) },
        { key: SupportLinkKey.Contact, label: t('nav.contact'), icon: SUPPORT_LINK_ICON[SupportLinkKey.Contact], desc: t('nav.contactDesc'), to: localePath(SITE_CONTACT_PATH) },
      ],
    },
  )
  return items
}
