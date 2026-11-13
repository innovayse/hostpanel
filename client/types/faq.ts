/**
 * FAQ type: one question served by `GET /api/public/faqs`.
 *
 * @module types/faq
 */

import type { FaqCategory } from '~/types/faqcategory'

/**
 * A frequently asked question as the storefront receives it, already localised by the API.
 * The category is the enum name the API serializes, which is the value of {@link FaqCategory}.
 */
export interface FAQ {
  /** Question identifier. */
  id: number
  /** Topic the question belongs to; the tab filter. */
  category: FaqCategory
  /** The question in the request language. */
  question: string
  /** The answer in the request language. */
  answer: string
  /** Display order, ascending. */
  sortOrder: number
}
