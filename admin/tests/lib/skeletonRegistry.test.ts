/**
 * Tests for the skeleton registry: a screen's schema is found by its file, and `withSkeletons` hangs it on the route
 * record whose loader was tagged with that file, nested records included.
 */
import { describe, expect, it } from 'vitest'
import type { RouteRecordRaw } from 'vue-router'
import { withViewFile } from '@/lib/lazyRoute'
import { skeletonSchemaOf, withSkeletons } from '@/lib/skeletonRegistry'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'

/**
 * Builds a loader tagged with a file, as the router's `lazy` does.
 *
 * @param file - The file under `src/` the loader stands for.
 * @returns The tagged loader.
 */
const loaderOf = (file: string): (() => Promise<unknown>) => withViewFile(() => Promise.resolve({}), file)

describe('skeletonSchemaOf', () => {
  it('finds the schema module beside a layout by the layout file', () => {
    const schema = skeletonSchemaOf('layouts/ClientLayout.vue')

    expect(schema?.kind).toBe(SkeletonKind.Tabs)
  })

  it('finds nothing for a file with no schema', () => {
    expect(skeletonSchemaOf('views/LoginView.vue')).toBeUndefined()
    expect(skeletonSchemaOf('views/NoSuchView.vue')).toBeUndefined()
  })
})

describe('withSkeletons', () => {
  it('puts the schema on the record of a tagged loader and keeps the rest of its meta', () => {
    const routes: RouteRecordRaw[] = [
      { path: '/clients/:id', component: loaderOf('layouts/ClientLayout.vue'), meta: { requiresAuth: true } },
    ]

    const result = withSkeletons(routes)

    expect(result[0]?.meta?.requiresAuth).toBe(true)
    expect(result[0]?.meta?.skeleton).toBe(skeletonSchemaOf('layouts/ClientLayout.vue'))
  })

  it('leaves the table it is given as it was', () => {
    const routes: RouteRecordRaw[] = [{ path: '/clients/:id', component: loaderOf('layouts/ClientLayout.vue'), meta: { requiresAuth: true } }]

    const result = withSkeletons(routes)

    expect(result).not.toBe(routes)
    expect(routes[0]?.meta?.skeleton).toBeUndefined()
  })

  it('reaches nested records and leaves the ones without a schema, a tag or a component alone', () => {
    const routes: RouteRecordRaw[] = [
      {
        path: '/reports',
        component: loaderOf('layouts/ReportsLayout.vue'),
        children: [
          { path: 'a', component: loaderOf('views/LoginView.vue') },
          { path: 'b', component: () => Promise.resolve({}) },
          { path: 'c', redirect: '/reports/a' },
        ],
      },
    ]

    const result = withSkeletons(routes)

    expect(result[0]?.meta?.skeleton).toBeDefined()
    const [noSchema, untagged, redirect] = result[0]?.children ?? []
    expect(noSchema?.meta).toBeUndefined()
    expect(untagged?.meta).toBeUndefined()
    expect(redirect?.meta).toBeUndefined()
  })
})
