import { COLOR_MODE_COOKIE, SYSTEM_MODE_BOOTSTRAP, parseColorMode } from '~/utils/colorMode'
import { resolveTemplateName } from '~/templates/types'
import type { TemplateName } from '~/templates/types'

/**
 * Page background per template and mode, for `<meta name="theme-color">` — the colour
 * the browser paints its own chrome on mobile. Read from the template stylesheets'
 * page tokens; classic has no light palette and stays dark either way.
 */
const PAGE_COLORS: Record<TemplateName, { dark: string, light: string }> = {
  aurora: { dark: '#08090f', light: '#f4f5fa' },
  nova: { dark: '#08191f', light: '#f4f8fa' },
  classic: { dark: '#0a0a0f', light: '#0a0a0f' },
}

/**
 * Colour-mode plugin: the one writer of the `dark` / `light` class on `<html>`.
 *
 * Replaces a client-only plugin that forced `dark` on every public page and a blocking
 * script in `app.vue` that did the same, both of which overrode the toggle in the
 * client area and made the operator's choice impossible. The class now comes from
 * `useAppColorMode().applied` through `useHead`, so the server renders it into the
 * HTML — no flash — and the browser keeps it in step with the toggle.
 *
 * Under `system` with no cookie the server cannot know the mode, so it emits no class
 * and the inline {@link SYSTEM_MODE_BOOTSTRAP} picks one before first paint; this
 * plugin then reads that class back into state so hydration does not undo it, and
 * follows the device preference for as long as the visitor has not chosen.
 *
 * Runs after `portal-settings`, which loads the operator's default.
 */
export default defineNuxtPlugin({
  name: 'color-mode',
  dependsOn: ['portal-settings'],
  setup() {
    const { applied, mode, choose } = useAppColorMode()
    const { get } = usePortalSettings()

    if (import.meta.client) {
      // One-off migration from the previous storage. A visitor who chose a mode
      // before the cookie existed keeps it; the key is removed so this runs once,
      // which is also why the legacy value can safely win over anything else here.
      try {
        const legacy = parseColorMode(localStorage.getItem(COLOR_MODE_COOKIE))
        if (legacy) {
          choose(legacy)
          localStorage.removeItem(COLOR_MODE_COOKIE)
        }
      } catch {
        // Storage can throw in a private window; the cookie path still works.
      }

      // The bootstrap script has already put the device's mode on <html>. Read it
      // back before hydration so useHead below does not strip it.
      if (applied.value === null) {
        applied.value = document.documentElement.classList.contains('light') ? 'light' : 'dark'
      }

      // Follow the device while the visitor has not chosen; a saved choice wins.
      const media = window.matchMedia('(prefers-color-scheme: dark)')
      media.addEventListener('change', (e) => {
        if (mode.value === 'system') applied.value = e.matches ? 'dark' : 'light'
      })
    }

    const template = computed(() => resolveTemplateName(get('portal.template', 'portalTemplate')))
    const themeColor = computed(() => PAGE_COLORS[template.value][applied.value ?? 'dark'])

    useHead({
      htmlAttrs: {
        class: () => applied.value ?? '',
      },
      meta: [
        // Tells the browser both schemes are supported, so form controls and
        // scrollbars follow the page rather than the OS.
        { name: 'color-scheme', content: 'dark light' },
        { name: 'theme-color', content: themeColor },
      ],
      // Emitted only when the server could not decide. With a class already in the
      // HTML the script would be dead weight, and a fixed operator mode must not be
      // overridden by the device.
      script: import.meta.server && applied.value === null
        ? [{ innerHTML: SYSTEM_MODE_BOOTSTRAP, tagPosition: 'head', tagPriority: 'critical' }]
        : [],
    })
  },
})
