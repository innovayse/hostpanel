/**
 * The app-launcher entries the header offers.
 *
 * The launcher points at sibling products (task tracker, ERP, webmail and so on)
 * that only exist in a deployment running them. Every URL in `runtimeConfig` has
 * a `*.local` development default, so "has a value" cannot decide whether an app
 * is really there; the launcher is therefore off unless an operator turns it on
 * with `portal.apps.enabled`, and each entry still has to resolve to a URL of
 * its own before it renders. A stock install shows no launcher rather than eight
 * links into nothing.
 */

import type { PortalApp } from '~/types/portalapp'

/**
 * Static part of each entry: identity, tile styling and where its URL comes
 * from. `envKey` names a `runtimeConfig.public` key; entries with none are
 * reachable only by setting `portal.apps.<id>` in the admin panel.
 */
const APP_DEFS: ReadonlyArray<{
  id: string
  tag: string
  tint: string
  envKey: string
  path?: string
}> = [
  { id: 'account',   tag: 'Ac', tint: 'linear-gradient(135deg, #7DE3FF, #5D3FFF)', envKey: 'baseUrl', path: '/account' },
  { id: 'tasks',     tag: 'Tk', tint: 'linear-gradient(135deg, #38E8A0, #00D1FF)', envKey: 'tasksUrl' },
  { id: 'erp',       tag: 'Er', tint: 'linear-gradient(135deg, #B9A6FF, #5D3FFF)', envKey: 'erpUrl' },
  { id: 'hostpanel', tag: 'Hp', tint: 'linear-gradient(135deg, #FF9CE0, #B9A6FF)', envKey: 'baseUrl', path: '/client' },
  { id: 'sheets',    tag: 'Sh', tint: 'linear-gradient(135deg, #00D1FF, #38E8A0)', envKey: 'sheetsUrl' },
  { id: 'mail',      tag: 'Ml', tint: 'linear-gradient(135deg, #5D3FFF, #00D1FF)', envKey: 'emailUrl' },
  { id: 'docs',      tag: 'Dc', tint: 'linear-gradient(135deg, #7DE3FF, #B9A6FF)', envKey: '' },
  { id: 'calendar',  tag: 'Cl', tint: 'linear-gradient(135deg, #FF9CE0, #5D3FFF)', envKey: '' },
]

/**
 * Whether a deployment has opted into the launcher.
 *
 * The admin setting arrives as text, but Nuxt coerces `NUXT_PUBLIC_*` values, so
 * the environment side is a real boolean by the time it is read. Both have to be
 * accepted, or switching the launcher on from the environment silently does
 * nothing.
 *
 * @param setting Value of `portal.apps.enabled`, empty when unset.
 * @param fromConfig Value of `runtimeConfig.public.portalAppsEnabled`.
 */
export const isLauncherEnabled = (setting: string, fromConfig: unknown): boolean => {
  const truthy = (value: string) => /^(true|1|yes|on)$/i.test(value.trim())

  if (setting) return truthy(setting)
  if (typeof fromConfig === 'boolean') return fromConfig
  return typeof fromConfig === 'string' && truthy(fromConfig)
}

/**
 * Final URL for one app, or empty when the deployment has none for it.
 *
 * A per-app setting holds a complete URL and wins outright; the environment
 * fallback names only a host the deployment already knows, so the path the app
 * lives under is appended to that rather than to the operator's own value.
 *
 * @param override Operator-set `portal.apps.<id>` value.
 * @param base URL from `runtimeConfig.public`.
 * @param path Path the app lives under, for hosts shared with other apps.
 */
export const resolveAppUrl = (override: string, base: string, path = ''): string => {
  if (override) return override.trim()
  if (!base) return ''
  return `${base.trim().replace(/\/+$/, '')}${path}`
}

/**
 * Resolves the launcher entries for the current deployment.
 *
 * @returns The entries to show — empty when the launcher is switched off.
 */
export const usePortalApps = () => {
  const { get } = usePortalSettings()
  const config = useRuntimeConfig()
  const { t } = useI18n()

  /**
   * The deployment's URL for an app, before any per-app setting overrides it.
   *
   * @param envKey Key in `runtimeConfig.public`, or empty for a settings-only app.
   */
  const fromEnv = (envKey: string): string => {
    if (!envKey) return ''
    const value = config.public[envKey as keyof typeof config.public]
    return typeof value === 'string' ? value.trim() : ''
  }

  const enabled = computed(() =>
    isLauncherEnabled(get('portal.apps.enabled', ''), config.public.portalAppsEnabled))

  const apps = computed<PortalApp[]>(() => {
    if (!enabled.value) return []

    return APP_DEFS.flatMap(def => {
      const url = resolveAppUrl(get(`portal.apps.${def.id}`, ''), fromEnv(def.envKey), def.path)
      if (!url) return []

      return [{
        id: def.id,
        tag: def.tag,
        tint: def.tint,
        label: t(`aurora.apps.items.${def.id}.label`),
        desc: t(`aurora.apps.items.${def.id}.desc`),
        url,
      }]
    })
  })

  return { apps }
}
