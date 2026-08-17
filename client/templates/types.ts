/** Every template the portal can render. The first entry is the shipped default. */
export const TEMPLATE_NAMES = ['aurora', 'classic'] as const

/** A template the portal can render. */
export type TemplateName = (typeof TEMPLATE_NAMES)[number]

/**
 * A renderable position a template must supply.
 * `header` and `footer` are resolved by the layout; the rest are page bodies.
 */
export type TemplateSlot =
  | 'header'
  | 'footer'
  | 'home'
  | 'hosting'
  | 'domains'
  | 'checkout'

/** The template used when configuration is absent or unrecognised. */
export const DEFAULT_TEMPLATE: TemplateName = 'aurora'

/**
 * Narrows an untrusted configuration value to a template name.
 *
 * Anything unrecognised — a typo in an admin settings field, an unset
 * environment variable, a template removed from a later release — resolves to
 * {@link DEFAULT_TEMPLATE} so a bad value degrades to the default design
 * instead of leaving the site with nothing to render.
 *
 * @param input Raw configured value.
 * @returns A template name that is guaranteed to exist in the registry.
 */
export function resolveTemplateName(input: string | null | undefined): TemplateName {
  return TEMPLATE_NAMES.includes(input as TemplateName)
    ? (input as TemplateName)
    : DEFAULT_TEMPLATE
}
