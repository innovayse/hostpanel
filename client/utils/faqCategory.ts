/**
 * FAQ topic slugs: the one place the URL / locale-key spelling of a {@link FaqCategory} is decided.
 *
 * The enum values equal the names the API serializes (`General`, `Products`); the public URLs
 * (`/faq/general`), the `faq.categories.*` / `seo.faqCategory.*` locale keys and the test ids use
 * the lowercase slug. Every page and component maps between the two here and nowhere else.
 *
 * @module utils/faqCategory
 */

import { FaqCategory } from '~/types/faqcategory'

/**
 * The lowercase slug of a topic, as used in URLs, locale keys and test ids.
 *
 * @param category - The topic.
 * @returns The slug, e.g. `products`.
 */
export const faqCategorySlug = (category: FaqCategory): string => category.toLowerCase()

/**
 * The topic a URL slug names.
 *
 * @param slug - The `[category]` route parameter.
 * @returns The topic, or `undefined` when the slug names none (a 404).
 */
export const faqCategoryFromSlug = (slug: string): FaqCategory | undefined =>
  Object.values(FaqCategory).find(category => faqCategorySlug(category) === slug)

/**
 * The topic a `[category]` route parameter names; an array parameter (a repeated segment) counts by its first entry.
 *
 * @param param - The raw `route.params.category` value.
 * @returns The topic, or `undefined` when the parameter is absent or names none (a 404).
 */
export const faqCategoryFromParam = (param: string | string[] | undefined): FaqCategory | undefined =>
  faqCategoryFromSlug(Array.isArray(param) ? (param[0] ?? '') : (param ?? ''))
