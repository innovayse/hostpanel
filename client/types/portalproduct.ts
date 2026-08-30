/**
 * One catalogue product, as `GET /api/portal/public/products` returns it.
 *
 * The route spreads the C# `ProductDto` verbatim and adds exactly two things: `pid`, a
 * WHMCS-compatible alias of `id`, and a `pricing.USD` block replacing `ProductPricingDto`'s
 * `{ monthly, annual }` numbers with decimal strings under all six billing-cycle keys. Every
 * other field below is `ProductDto`'s own, camelCase, as `System.Text.Json` writes it.
 *
 * ## `groupId`, not `gid`
 *
 * This declared `gid`, and the public site reads `Number(p.gid)` in four places to group plans
 * into product families. `ProductDto` has always sent `groupId`; nothing anywhere adds `gid` to
 * a product (the BFF's `gid` handling is a *query* parameter it translates to `groupId` on the
 * way out, not a response field). So `Number(p.gid)` was `NaN` on every product, no group ever
 * matched, and `components/sections/Products.vue` dropped every family it tried to build.
 *
 * ## There are no `translated_*` fields, and nothing reads them any more
 *
 * `translated_name`, `translated_tagline`, `translated_shortdescription`,
 * `translated_description`, `group_translations` and `group_features` used to be read across
 * `pages/products/`, `pages/hosting/`, `pages/trial/`, `components/sections/Products.vue`,
 * `templates/classic/pages/Hosting.vue` and `utils/whmcs.ts`. `ProductDto` carries none of
 * them, `GET /api/products` was checked against a live backend and sends none of them, and
 * every read sat in front of an `|| p.name` / `|| p.description` fallback that was the only
 * branch ever taken. They are gone, along with the `lang` parameter the BFF forwarded to feed
 * them — see `server/api/portal/public/products.get.ts`.
 *
 * **Localised product copy does not exist in the backend.** There is no product translations
 * table, no `lang` on `ProductsController`, nothing under `Application/Products/`. `Slide` is
 * the one entity with a `*Translation` sibling (`SlideTranslation`), and
 * `useCatalogApi().loadSlides(lang)` is the one place a `lang` parameter still means anything.
 * Making product copy translatable starts there — a `ProductTranslation` entity and a locale
 * on the query — and only then does a field belong back on this interface.
 *
 * **`group_features` already has a successor.** Structured, per-product specification lines
 * are `ProductFeature` (label / value / sortOrder), served by
 * `GET /api/portal/public/product-features` and read through
 * `useCatalogApi().loadProductFeatures(groupId)`. A caller that wanted `group_features` wants
 * that endpoint.
 *
 * @module types/portalproduct
 */

/** Prices for one currency, as the BFF's `pricing` block carries them. */
export interface PortalProductPricing {
  /** Symbol printed before an amount. */
  prefix?: string
  /** Symbol printed after an amount. */
  suffix?: string
  /** Monthly price as a decimal string; `-1.00` when the cycle is not offered. */
  monthly?: string
  /** Quarterly price; the BFF sends `-1.00` — the backend prices only monthly and annually. */
  quarterly?: string
  /** Semi-annual price; `-1.00`, as above. */
  semiannually?: string
  /** Annual price as a decimal string; `-1.00` when the cycle is not offered. */
  annually?: string
  /** Biennial price; `-1.00`, as above. */
  biennially?: string
  /** Triennial price; `-1.00`, as above. */
  triennially?: string
}

/** One catalogue product. */
export interface PortalProduct {
  /** Product primary key. */
  id: number
  /** WHMCS-compatible alias of {@link PortalProduct.id}, added by the proxy. */
  pid?: number
  /** Product group id this product belongs to. */
  groupId: number
  /** Product name. */
  name: string
  /** Marketing description; the feature bullets are parsed out of it. */
  description?: string | null
  /** Landing-page URL for the product, when it has one. */
  website?: string | null
  /** URL slug, when the product has one. */
  slug?: string | null
  /** Hosting package name used for provisioning. */
  packageName?: string | null
  /** Product type, as `ProductType` serialises. */
  type?: string
  /** Current status, as `ProductStatus` serialises. */
  status?: string
  /** FK to the server group used for provisioning. */
  serverGroupId?: number | null
  /**
   * Prices keyed by currency code. Only `USD` is populated: the BFF hardcodes the key and the
   * `$` prefix, so this is not the account's billing currency and must not be read as one.
   */
  pricing?: Partial<Record<string, PortalProductPricing>>
}
