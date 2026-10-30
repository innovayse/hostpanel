/**
 * The icon of a product family, by the type of service its products sell.
 *
 * Every name is spelled out in full on purpose: the icon module bundles the names it finds as
 * literals in the source, so a name built at run time (`mdi:${x}`) would render an empty tile.
 *
 * @module constants/productTypeIcon
 */

import { ProductType } from '~/types/producttype'

/** Icon used when a family's type is unknown or has no icon of its own. */
export const DEFAULT_PRODUCT_ICON = 'mdi:cube-outline'

/** Icon of each product type. */
export const PRODUCT_TYPE_ICON: Readonly<Record<ProductType, string>> = {
  [ProductType.SharedHosting]: 'mdi:cloud-check',
  [ProductType.Vps]: 'mdi:server-network',
  [ProductType.Dedicated]: 'mdi:server',
  [ProductType.Domain]: 'mdi:earth',
  [ProductType.Ssl]: 'mdi:shield-lock',
  [ProductType.Email]: 'mdi:email-outline',
  [ProductType.Addon]: 'mdi:puzzle-outline',
  [ProductType.Backup]: 'mdi:backup-restore',
  [ProductType.Digital]: 'mdi:download-box',
  [ProductType.Other]: DEFAULT_PRODUCT_ICON,
}

/**
 * The icon of a product type.
 *
 * @param type The type as the API sent it; may be missing, or a value newer than this build knows.
 * @returns The type's icon, or {@link DEFAULT_PRODUCT_ICON} when there is none.
 */
export const productTypeIcon = (type: ProductType | null | undefined): string =>
  (type ? PRODUCT_TYPE_ICON[type] : undefined) ?? DEFAULT_PRODUCT_ICON
