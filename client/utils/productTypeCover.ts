/**
 * The generated cover illustration of a product family, by the type of service its products sell.
 *
 * The illustrations are single-colour silhouettes shipped as client assets (`public/covers/*.svg`),
 * so every install has them. A card paints one as a CSS mask over a block filled with the family
 * accent, which is why the same file reads in light and dark and takes any family colour. Every
 * class is spelled out in full: Tailwind emits only literals it finds in the scanned folders (this one is under utils/ for that reason; constants/ is not scanned), so a class
 * built at run time (`mask-[url(/covers/${x}.svg)]`) would paint nothing.
 *
 * @module utils/productTypeCover
 */

import { ProductType } from '~/types/producttype'
import { SlideCoverKind } from '~/types/slidecoverkind'

/**
 * Mask classes shared by every cover: centred, scaled to fit whole, no tiling. `contain`, not `cover`:
 * the illustrations are drawn on a 400 x 160 canvas and the cover box is far wider than that on a
 * large card, so `cover` blew the art up to the box width and cut its top and bottom off. Fitting it
 * whole shows every illustration in full at any card width; the rest of the box keeps its tint.
 */
const COVER_MASK_BASE = '[mask-size:contain] [mask-position:center] [mask-repeat:no-repeat]'

/** Cover used when a family's type is unknown or has no illustration of its own. */
export const DEFAULT_COVER_CLASS = `[mask-image:url(/covers/generic.svg)] ${COVER_MASK_BASE}`

/** Mask classes of each product type's cover illustration. */
export const PRODUCT_TYPE_COVER: Readonly<Record<ProductType, string>> = {
  [ProductType.SharedHosting]: `[mask-image:url(/covers/hosting.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Vps]: `[mask-image:url(/covers/vps.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Dedicated]: `[mask-image:url(/covers/dedicated.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Domain]: `[mask-image:url(/covers/domain.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Ssl]: `[mask-image:url(/covers/ssl.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Email]: `[mask-image:url(/covers/email.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Addon]: `[mask-image:url(/covers/addon.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Backup]: `[mask-image:url(/covers/backup.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Digital]: `[mask-image:url(/covers/digital.svg)] ${COVER_MASK_BASE}`,
  [ProductType.Other]: DEFAULT_COVER_CLASS,
}

/**
 * The cover classes of a product type.
 *
 * @param type The type as the API sent it; may be missing, or a value newer than this build knows.
 * @returns The type's mask classes, or the generic cover's when there is none.
 */
export const productTypeCover = (type: ProductType | null | undefined): string =>
  (type ? PRODUCT_TYPE_COVER[type] : undefined) ?? DEFAULT_COVER_CLASS

/** The cover of a home-page slide: its own picture, or the illustration of its topic. */
export type SlideCover =
  | { kind: SlideCoverKind.Image, src: string }
  | { kind: SlideCoverKind.Art, classes: string }

/** Storefront path segments that name a product family or type, by the type they sell. */
const SLIDE_LINK_TYPES: ReadonlyArray<readonly [string, ProductType]> = [
  ['hosting', ProductType.SharedHosting],
  ['vps', ProductType.Vps],
  ['dedicated', ProductType.Dedicated],
  ['domain', ProductType.Domain],
  ['ssl', ProductType.Ssl],
  ['email', ProductType.Email],
  ['mail', ProductType.Email],
  ['backup', ProductType.Backup],
  ['addon', ProductType.Addon],
]

/**
 * The product type a slide's link points at, read from the first segment of its path
 * (`/vps`, `/en/domains`, `/products/ssl`): the language prefix and `products` are skipped.
 *
 * @param link The slide's learn-more or call-to-action link; may be missing.
 * @returns The type the link names, or null when it names none.
 */
export const slideLinkType = (link: string | null | undefined): ProductType | null => {
  const segments = ((link ?? '').split(/[?#]/)[0] ?? '').split('/').filter(Boolean)
  const topic = segments.find((segment) => segment !== 'products' && !/^[a-z]{2}$/.test(segment))
  if (!topic) {
    return null
  }
  return SLIDE_LINK_TYPES.find(([stem]) => topic.toLowerCase().startsWith(stem))?.[1] ?? null
}

/**
 * Chooses the cover beside a slide, in this order: the slide's uploaded picture; the illustration of
 * the product type its link points at; the generic hosting illustration.
 *
 * @param image The slide's uploaded picture URL; empty or missing when there is none.
 * @param link The slide's learn-more or call-to-action link.
 * @returns The cover to draw.
 */
export const slideCover = (image: string | null | undefined, link: string | null | undefined): SlideCover => {
  if (image && image.trim()) {
    return { kind: SlideCoverKind.Image, src: image }
  }
  return { kind: SlideCoverKind.Art, classes: productTypeCover(slideLinkType(link)) }
}
