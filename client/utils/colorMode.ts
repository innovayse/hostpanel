/**
 * Colour-mode resolution, kept free of Nuxt so it can be unit-tested and so the
 * rule lives in one place. `useAppColorMode()` and `plugins/color-mode.ts` are the
 * only callers.
 *
 * Resolution order, deliberately: the visitor's saved choice first, the operator's
 * default second, the device preference last. That order is what lets an operator
 * ship a light storefront from the admin panel without a redeploy while a returning
 * visitor who chose otherwise keeps their choice.
 *
 * @module utils/colorMode
 */

import { SETTING_OFF } from '~/constants/settingValue'
import { ColorMode } from '~/types/colormode'
import { ColorModeSystem } from '~/types/colormodesystem'
import type { ColorModeSetting } from '~/types/colormodesetting'

/** The cookie that carries a visitor's explicit choice. Absent means "no choice made". */
export const COLOR_MODE_COOKIE = 'color-mode'

/** How long a visitor's choice is kept, in seconds: one year. */
export const COLOR_MODE_COOKIE_MAX_AGE = 60 * 60 * 24 * 365

/**
 * The mode a fresh install starts in. Matches what the storefront did before the
 * operator setting existed, so an upgrade changes nothing a visitor sees.
 */
export const DEFAULT_COLOR_MODE_SETTING: ColorModeSetting = ColorMode.Dark

/**
 * Narrows an untrusted string to a colour mode, or `null` when it is not one.
 *
 * @param input Raw cookie or setting value.
 * @returns The matching {@link ColorMode}, or `null`.
 */
export const parseColorMode = (input: string | null | undefined): ColorMode | null => {
  if (input === ColorMode.Light) {
    return ColorMode.Light
  }
  return input === ColorMode.Dark ? ColorMode.Dark : null
}

/**
 * Narrows an untrusted operator setting to something this module understands.
 *
 * Anything unrecognised (an unset row, an older backend, a typo that got past the
 * API) degrades to {@link DEFAULT_COLOR_MODE_SETTING} rather than to no theme at all.
 *
 * @param input Raw `portal.theme.default` value.
 * @returns A usable setting.
 */
export const parseColorModeSetting = (input: string | null | undefined): ColorModeSetting => {
  return input === ColorModeSystem.System ? ColorModeSystem.System : (parseColorMode(input) ?? DEFAULT_COLOR_MODE_SETTING)
}

/**
 * Decides the mode the server can render.
 *
 * Returns `null` only when nothing but the device knows, no cookie and an operator
 * default of `system`, in which case the server emits no class and a head script
 * (see {@link SYSTEM_MODE_BOOTSTRAP}) picks one before first paint.
 *
 * @param cookie The visitor's saved choice, if any.
 * @param operatorDefault The operator's `portal.theme.default`.
 * @returns The mode to render, or `null` to defer to the device.
 */
export const resolveColorMode = (
  cookie: string | null | undefined,
  operatorDefault: ColorModeSetting,
): ColorMode | null => {
  const chosen = parseColorMode(cookie)
  if (chosen) {
    return chosen
  }
  return operatorDefault === ColorModeSystem.System ? null : operatorDefault
}

/**
 * Reads `portal.theme.user_toggle`. Only the stored off value (`SETTING_OFF`) hides the switch: an
 * unset row, an older backend or anything else keeps today's behaviour, which is a
 * visible toggle.
 *
 * @param input Raw setting value.
 * @returns Whether visitors may switch modes.
 */
export const isUserToggleEnabled = (input: string | null | undefined): boolean => {
  return input !== SETTING_OFF
}

/**
 * Inline head script for the `system` case: applies the device preference to `<html>`
 * before the first paint, so a light-mode visitor never sees a dark frame.
 *
 * Both template stylesheets declare their dark palette on bare `:root`, a page with no
 * class is dark, so without this a `system` visitor on a light device would flash dark
 * until hydration. It runs only when the server could not decide (see
 * {@link resolveColorMode}); with a cookie or a fixed operator default the class is in
 * the server-rendered HTML and no script is emitted.
 */
export const SYSTEM_MODE_BOOTSTRAP =
  '(function(){try{var d=window.matchMedia("(prefers-color-scheme: dark)").matches;'
  + 'var c=document.documentElement.classList;c.add(d?"dark":"light");c.remove(d?"light":"dark")}catch(e){}})()'
