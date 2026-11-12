/**
 * Helpers for mounting a storefront section (testimonials, partners, projects, FAQs, locations)
 * against the real store, API composable and transport: the stub answers the public read the
 * section starts, and the mount waits until that read has arrived and its answer has been rendered.
 *
 * A section starts its read from `useAsyncData` without awaiting it, and the read crosses a real
 * socket, so `flushPromises` alone would return before the answer. These helpers wait on the
 * stub's recorded requests instead.
 *
 * @module tests/helpers/storefrontSection
 */

import type { VueWrapper } from '@vue/test-utils'
import { requestsTo } from './stubRoutes'
import { until, untilStable } from './settle'
import type { StubServer } from './stubServer'

/** The public storefront reads, by content kind. */
export const StorefrontPath = {
  Testimonials: '/api/portal/public/testimonials',
  Partners: '/api/portal/public/partners',
  Locations: '/api/portal/public/locations',
  Projects: '/api/portal/public/projects',
  Faqs: '/api/portal/public/faqs',
} as const

/**
 * Mounts a section and waits until the reads it starts have been answered and rendered.
 *
 * @param stub - The running stub.
 * @param paths - The public reads the mounted section is expected to start.
 * @param mount - Mounts the section and returns its wrapper.
 * @returns The wrapper, once every listed read has arrived and its answer has rendered.
 * @throws When a listed read never arrives at the stub.
 */
export const mountAfterReads = async (stub: StubServer, paths: string[], mount: () => VueWrapper): Promise<VueWrapper> => {
  const before = paths.map(path => requestsTo(stub.requests, path).length)
  const wrapper = mount()
  for (const [index, path] of paths.entries()) {
    await until(() => requestsTo(stub.requests, path).length > (before[index] ?? 0), `a read of ${path}`)
  }
  await untilStable(() => wrapper.html(), 'the section to finish rendering the answers')
  return wrapper
}
