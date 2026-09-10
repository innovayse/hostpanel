import { pickBrandLogo, pickBrandMark, resolveBrandLogos } from '~/utils/brandLogo'

/**
 * The operator's logo, in every form a template needs.
 *
 * The only place the light/dark/mark rule lives. Three headers and a footer used to
 * each compute `logoUrl` from `portal.logo` on their own, and none could react to the
 * colour mode; with a dark variant that choice is a rule, and a rule in five files is
 * five bugs.
 *
 * A header renders **both** wordmarks ({@link light} and {@link dark}) and lets the
 * `dark:` variant pick — never the mode-aware {@link logo}. Under the `system`
 * default the server does not know the mode and renders as dark; production Vue does
 * not patch an `src` that disagrees after hydration, so a JavaScript pick would leave
 * a light-device visitor looking at the dark logo. Two images and a class swap cannot
 * disagree with the palette. {@link logo} exists for code that runs only in the
 * browser after the mode is settled.
 *
 * Every value is `''` when the operator has uploaded nothing, and a template renders
 * its built-in mark and wordmark then — exactly as before this composable existed.
 *
 * @returns Reactive logo URLs and the alt text to render them with.
 */
export const useBrandLogo = () => {
  const { get } = usePortalSettings()
  const { isDark } = useAppColorMode()
  const { name } = useSiteIdentity()

  const sources = computed(() => ({
    light: get('portal.logo', 'portalLogo'),
    dark: get('portal.logo.dark', 'portalLogoDark'),
    mark: get('portal.logo.mark', 'portalLogoMark'),
  }))

  const pair = computed(() => resolveBrandLogos(sources.value))

  /** The wordmark for light backgrounds, or `''`. Render with `dark:hidden` when {@link hasDarkVariant}. */
  const light = computed(() => pair.value.light)

  /** The wordmark for dark backgrounds, or `''`. Render with `hidden dark:block` when {@link hasDarkVariant}. */
  const dark = computed(() => pair.value.dark)

  /** Whether {@link light} and {@link dark} differ, so both images are needed. */
  const hasDarkVariant = computed(() => pair.value.hasDarkVariant)

  /** The wordmark for the mode the browser is in right now, or `''`. Browser-only decisions. */
  const logo = computed(() => pickBrandLogo(sources.value, isDark.value))

  /**
   * The wordmark for a surface that is always dark whatever the page mode — the
   * sign-in pages paint their own `#0a0a0f` — so the light logo would sit on black.
   */
  const logoOnDark = computed(() => pair.value.dark)

  /** The compact logo for narrow places, or `''`. */
  const mark = computed(() => pickBrandMark(sources.value, isDark.value))

  /**
   * Alt text for either image. The logo *is* the site name; the empty `alt=""` the
   * headers shipped with told a screen reader the brand was decorative.
   */
  const alt = name

  /** Whether the operator has uploaded any logo at all. */
  const isCustom = computed(() => Boolean(sources.value.light || sources.value.dark || sources.value.mark))

  return { light, dark, hasDarkVariant, logo, logoOnDark, mark, alt, isCustom }
}
