import type { TemplateName, TemplateSlot } from './types'

/** Defers loading a template component until the slot is actually rendered. */
export type TemplateLoader = () => Promise<unknown>

/**
 * The single place a template name and slot resolve to a component.
 *
 * Loaders are dynamic imports so the browser only fetches the active
 * template's components, and so a component can be added without any
 * string-based lookup against Nuxt's auto-import namespace.
 */
export const templates: Record<TemplateName, Record<TemplateSlot, TemplateLoader>> = {
  aurora: {
    header: () => import('~/templates/aurora/layout/Header.vue'),
    footer: () => import('~/templates/aurora/layout/Footer.vue'),
    home: () => import('~/templates/aurora/pages/Home.vue'),
    hosting: () => import('~/templates/aurora/pages/Hosting.vue'),
    domains: () => import('~/templates/aurora/pages/Domains.vue'),
    checkout: () => import('~/templates/aurora/pages/Checkout.vue'),
  },
  classic: {
    header: () => import('~/templates/classic/layout/Header.vue'),
    footer: () => import('~/templates/classic/layout/Footer.vue'),
    home: () => import('~/templates/classic/pages/Home.vue'),
    hosting: () => import('~/templates/classic/pages/Hosting.vue'),
    domains: () => import('~/templates/classic/pages/Domains.vue'),
    checkout: () => import('~/templates/classic/pages/Checkout.vue'),
  },
}
