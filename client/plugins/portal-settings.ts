/**
 * Portal settings plugin.
 *
 * Loads the operator's `portal.*` settings once per request, before any layout
 * or page renders, so `useTemplate()` can resolve the active template during
 * server rendering rather than swapping designs after hydration.
 *
 * The payload transfers to the client with the rest of the SSR state, so the
 * browser does not repeat the request.
 *
 * Named so `plugins/color-mode.ts` can declare it as a dependency: plugins run in
 * file-name order otherwise, and `color-mode` sorts before `portal-settings`, which
 * would resolve the operator's default against an empty map.
 */
export default defineNuxtPlugin({
  name: 'portal-settings',
  async setup() {
    const settings = useState<Record<string, string>>('portalSettings', () => ({}))

    if (Object.keys(settings.value).length > 0) return

    try {
      settings.value = await $fetch<Record<string, string>>('/api/portal/public/settings')
    } catch {
      // The endpoint already swallows backend failures; this guards the request
      // itself. An empty map means every consumer uses its environment default.
      settings.value = {}
    }
  },
})
