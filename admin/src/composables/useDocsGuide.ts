import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { getRuntimeConfig } from '@/lib/runtimeConfig'
import { DOCS_GUIDES } from '@/lib/docsGuides'

/**
 * Returns the full URL of the documentation guide for the current route, or null when
 * the docs address is not configured or no guide is mapped to this screen.
 *
 * @returns An object with a `guideUrl` computed ref.
 */
export const useDocsGuide = (): { guideUrl: ReturnType<typeof computed<string | null>> } => {
  const route = useRoute()
  const { docsUrl } = getRuntimeConfig()

  const guideUrl = computed<string | null>(() => {
    if (!docsUrl) return null
    const path = DOCS_GUIDES[route.path]
    if (!path) return null
    return `${docsUrl}/${path}`
  })

  return { guideUrl }
}
