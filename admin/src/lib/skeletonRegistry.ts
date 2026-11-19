/**
 * The registry of skeleton schemas: finds the schema of a screen by its file and puts it on the route records.
 *
 * Every screen and layout that has a skeleton keeps it in a pure data module beside it, `views/<Name>.schema.ts` or
 * `layouts/<Name>.schema.ts`, whose default export is a `SkeletonSchema`. The modules are collected eagerly: a schema
 * holds only data, so it is cheap to load with the shell, and it has to be there before the chunk of its screen is.
 * {@link withSkeletons} then hangs each schema on the route record of its screen as `meta.skeleton`, which is what
 * `RoutedView` draws while that screen downloads. A screen whose content is unknown until it has loaded is named in
 * `lib/skeletonExceptions.ts` instead.
 */
import type { RouteRecordRaw } from 'vue-router'
import { viewFileOf } from '@/lib/lazyRoute'
import type { SkeletonSchema } from '@/utils/ui/skeletonSchema'

/** Every schema module of a view or a layout, keyed by its path relative to this file. */
const modules = import.meta.glob<{ default: SkeletonSchema }>('../{views,layouts}/*.schema.ts', { eager: true })

/**
 * Finds the schema of a screen.
 *
 * @param file - The screen's file under `src/`, such as `views/AddClientView.vue` or `layouts/ClientLayout.vue`.
 * @returns Its schema, or undefined when the screen has none.
 */
export const skeletonSchemaOf = (file: string): SkeletonSchema | undefined =>
  modules[`../${file.replace(/\.vue$/, '.schema.ts')}`]?.default

/**
 * Puts the schema of a screen on its own route record as `meta.skeleton`.
 *
 * A record is matched by the file its loader was tagged with (`withViewFile`); a record with no tagged loader (a
 * redirect, the eager layout) or with no schema is returned as it is.
 *
 * @param route - One route record, children not looked at.
 * @returns The record carrying its schema, or the record itself when it has none.
 */
const withSchema = (route: RouteRecordRaw): RouteRecordRaw => {
  const file = 'component' in route ? viewFileOf(route.component) : undefined
  const schema = file === undefined ? undefined : skeletonSchemaOf(file)
  return schema === undefined ? route : { ...route, meta: { ...route.meta, skeleton: schema } }
}

/**
 * Puts the schema of each lazy screen on its route record as `meta.skeleton`, nested records included.
 *
 * The table given is not changed: the result is built from new records.
 *
 * @param routes - The route table, built with tagged loaders.
 * @returns A table of the same shape whose records carry their schema.
 */
export const withSkeletons = (routes: RouteRecordRaw[]): RouteRecordRaw[] =>
  routes.map((route) => {
    const tagged = withSchema(route)
    return tagged.children === undefined ? tagged : { ...tagged, children: withSkeletons(tagged.children) }
  })
