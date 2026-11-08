/** The text of a slide in the reader's language. */
export interface SlideContent {
  /** Resolved translated title. */
  title: string
  /** Short tagline text. */
  tagline: string | null
  /** Longer description text. */
  description: string | null
  /** Feature bullet points. */
  features: string[] | null
}

/** The optional links a slide points to. */
export interface SlideLinks {
  /** Optional live demo URL. */
  demoUrl: string | null
  /** URL for the "Learn More" page. */
  learnMoreUrl: string | null
}

/** The call-to-action button of a slide in the reader's language. */
export interface SlideCallToAction {
  /** Call-to-action button text. */
  text: string | null
  /** Call-to-action button URL. */
  url: string | null
}

/** The price of the product a slide links to. */
export interface SlidePricing {
  /** Monthly price, if applicable. */
  monthlyPrice: number | null
  /** Annual price, if applicable. */
  annualPrice: number | null
  /** Lowest monthly equivalent across every billing cycle the product is priced in, if any. */
  fromPrice: number | null
}

/** One marketing slide, as `GET /api/portal/public/slides` returns it. */
export interface SlidePublic {
  /** Unique slide identifier. */
  id: number
  /** MDI icon name (without the `mdi:` prefix). */
  iconName: string
  /** Hex brand color for the slide. */
  brandColor: string
  /** Background image path. */
  imageUrl: string
  /** Associated product ID, if any. */
  productId: number | null
  /** Headline, tagline, description and feature bullets. */
  content: SlideContent
  /** Demo and learn-more links. */
  links: SlideLinks
  /** Call-to-action button text and URL. */
  callToAction: SlideCallToAction
  /** Monthly, annual and from price of the linked product. */
  pricing: SlidePricing
}
