/**
 * A storefront slide, its per-locale text, and the payloads that write one.
 */

import type { Product } from './product'

/** A single slide translation for a specific locale. */
export interface SlideTranslation {
  /** BCP-47 locale code (e.g. "en", "ru", "hy"). */
  locale: string
  /** Slide headline. */
  title: string
  /** Optional subtitle. */
  tagline: string | null
  /** Optional body text. */
  description: string | null
  /** Optional feature list. */
  features: string[] | null
  /** Optional CTA button label. */
  ctaText: string | null
  /** Optional CTA button link. */
  ctaUrl: string | null
}

/** Admin slide with all translations. */
export interface Slide {
  /** Slide ID. */
  id: number
  /** MDI icon name. */
  iconName: string
  /** Hex brand color. */
  brandColor: string
  /** Image URL or path. */
  imageUrl: string
  /** Optional demo link. */
  demoUrl: string | null
  /** Optional learn-more link. */
  learnMoreUrl: string | null
  /** Optional linked product ID. */
  productId: number | null
  /** Product name if linked. */
  productName: string | null
  /** Display order. */
  sortOrder: number
  /** Whether active. */
  isActive: boolean
  /** Target audience: All, Guest, Authenticated. */
  targetAudience: string
  /** Optional visibility start. */
  visibleFrom: string | null
  /** Optional visibility end. */
  visibleUntil: string | null
  /** Creation timestamp. */
  createdAt: string
  /** All locale translations. */
  translations: SlideTranslation[]
}

/** Payload for creating a new slide. */
export interface CreateSlidePayload {
  /** MDI icon name. */
  iconName: string
  /** Hex brand color. */
  brandColor: string
  /** Image URL or path. */
  imageUrl: string
  /** Optional demo link. */
  demoUrl: string | null
  /** Optional learn-more link. */
  learnMoreUrl: string | null
  /** Optional linked product ID. */
  productId: number | null
  /** Display order. */
  sortOrder: number
  /** Whether active. */
  isActive: boolean
  /** Target audience. */
  targetAudience: string
  /** Optional visibility start. */
  visibleFrom: string | null
  /** Optional visibility end. */
  visibleUntil: string | null
  /** Translations for all locales. */
  translations: SlideTranslation[]
}

/** Payload for updating a slide. */
export interface UpdateSlidePayload extends CreateSlidePayload {
  /** Slide ID. */
  id: number
}
