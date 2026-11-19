import type { ContentLanguage } from '@/locales/contentLanguage'

/** The editable words of one language version of an email template. */
export interface EmailTemplateContent {
  /** Email subject line. */
  subject: string
  /** HTML body content. */
  body: string
  /** What the template is for, null when the API has none. */
  description: string | null
}

/** One language version of an email template; a template has one per language at most. */
export interface EmailTemplate extends EmailTemplateContent {
  /** Unique identifier of this version. */
  id: number
  /** Template slug, the API's identifier of the template (the API sends no separate display name). */
  slug: string
  /** The language this version is written in. */
  language: ContentLanguage
  /** Whether the version is in use. */
  isActive: boolean
}

/** Body of the request that adds a language version to a template. */
export interface CreateEmailTemplatePayload extends EmailTemplateContent {
  /** Template slug the version belongs to. */
  slug: string
  /** The language the version is written in. */
  language: ContentLanguage
}

/** One template with all of its language versions, as the templates screen lists it. */
export interface EmailTemplateGroup {
  /** The template slug. */
  slug: string
  /** The stored versions, in the order the API listed them. */
  versions: EmailTemplate[]
  /** The version shown in the list: the default language's, or the first one stored. */
  shown: EmailTemplate
}
