/**
 * The meta members the admin router's records carry beyond the guards' own flags.
 *
 * `skeleton` is set on every lazy screen or layout that has a skeleton schema (see `lib/skeletonRegistry.ts`), so the
 * shell can draw the screen's skeleton while its chunk is still downloading. `shell` marks the record of the eager
 * layout, which the shell draws itself, rather than a skeleton, while its routed children download.
 */
import type { SkeletonSchema } from '@/utils/ui/skeletonSchema'

declare module 'vue-router' {
  /** Route meta members the admin sets and reads. */
  interface RouteMeta {
    /** What the screen of the record is made of; absent when the screen has no schema of its own. */
    skeleton?: SkeletonSchema
    /** True on the record whose component is the eager shell layout. */
    shell?: boolean
    /** Locale key naming a page that is outside the navigation, for the top bar's breadcrumb (`lib/breadcrumb.ts`). */
    crumb?: string
  }
}


