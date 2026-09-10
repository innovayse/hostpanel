import {
  COLOR_MODE_COOKIE,
  COLOR_MODE_COOKIE_MAX_AGE,
  DEFAULT_COLOR_MODE_SETTING,
  isUserToggleEnabled,
  parseColorMode,
  parseColorModeSetting,
  resolveColorMode,
} from '~/utils/colorMode'
import type { ColorMode, ColorModeSetting } from '~/utils/colorMode'

/**
 * The storefront's colour mode: what it is, whether the visitor may change it, and
 * how to change it.
 *
 * Resolution order — visitor's cookie, then the operator's `portal.theme.default`,
 * then the device — is implemented in `utils/colorMode.ts`; this composable only
 * binds it to Nuxt state. The `<html>` class itself is written by
 * `plugins/color-mode.ts` from {@link applied}, so no other code should touch
 * `documentElement.classList` — two writers is how the public site ended up pinned
 * dark while the client area had a working toggle.
 *
 * The cookie rather than `localStorage`: the server reads it during SSR and renders
 * the right class in the HTML, so a returning light-mode visitor gets a light first
 * paint instead of a dark frame corrected after hydration.
 *
 * @returns The current mode and a toggle.
 */
export const useAppColorMode = () => {
  const { get } = usePortalSettings()
  const cookie = useCookie<string | null>(COLOR_MODE_COOKIE, {
    sameSite: 'lax',
    maxAge: COLOR_MODE_COOKIE_MAX_AGE,
  })

  /** The operator's default, narrowed. */
  const operatorDefault = computed<ColorModeSetting>(() =>
    parseColorModeSetting(get('portal.theme.default', 'portalThemeDefault', DEFAULT_COLOR_MODE_SETTING)))

  /** Whether the sun/moon switch is rendered at all. */
  const userToggleEnabled = computed(() =>
    isUserToggleEnabled(get('portal.theme.user_toggle', 'portalThemeUserToggle', 'true')))

  /**
   * Whether the visitor has made a choice of their own. Shared state rather than the
   * cookie ref, because every caller of this composable gets its own cookie ref and
   * a choice made through one must be visible to the plugin's device listener at once.
   */
  const chosen = useState<boolean>('colorModeChosen', () => parseColorMode(cookie.value) !== null)

  /**
   * The mode actually on `<html>`. `null` only on the server under `system`, where
   * nobody knows yet; the plugin fills it in from the DOM as soon as it runs in the
   * browser, and from `matchMedia` when the device preference changes.
   */
  const applied = useState<ColorMode | null>('colorModeApplied', () =>
    resolveColorMode(cookie.value, operatorDefault.value))

  /**
   * The effective setting: the visitor's choice when they made one, else the
   * operator's default. `system` here means the device decides.
   */
  const mode = computed<ColorModeSetting>(() =>
    chosen.value && applied.value ? applied.value : operatorDefault.value)

  /**
   * Dark or not. Under `system` on the server this answers dark, matching the
   * class-less palette both templates declare on `:root`, and is corrected in the
   * browser before hydration.
   */
  const isDark = computed(() => (applied.value ?? 'dark') === 'dark')

  /**
   * Applies a mode and remembers it as the visitor's choice.
   *
   * @param next The mode to switch to.
   */
  function choose(next: ColorMode): void {
    applied.value = next
    chosen.value = true
    cookie.value = next
  }

  /**
   * Flips the mode and remembers the choice. A no-op when the operator has disabled
   * the switch: the button is not rendered then, but a stale bundle or a keyboard
   * shortcut must not be able to bypass the setting.
   */
  function toggle(): void {
    if (!userToggleEnabled.value) return
    choose(isDark.value ? 'light' : 'dark')
  }

  return { isDark, mode, applied, userToggleEnabled, choose, toggle }
}
