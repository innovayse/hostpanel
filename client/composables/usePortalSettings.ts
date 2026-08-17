/**
 * Operator-managed `portal.*` settings, with an environment fallback.
 *
 * Resolution order, deliberately: the admin-managed setting first, the
 * environment variable second, the caller's default last. That order is what
 * lets an operator change the storefront from the admin panel without a
 * redeploy, while a fresh install with no settings rows still renders.
 *
 * @returns A reader for individual settings.
 */
export const usePortalSettings = () => {
  const settings = useState<Record<string, string>>('portalSettings', () => ({}))
  const config = useRuntimeConfig()

  /**
   * Reads one setting.
   *
   * @param key Setting key, e.g. `portal.template`.
   * @param envKey Matching key in `runtimeConfig.public`, e.g. `portalTemplate`.
   * @param fallback Value when neither source has one.
   * @returns The resolved value, trimmed; empty string when nothing is set.
   */
  const get = (key: string, envKey: string, fallback = ''): string => {
    const fromSettings = settings.value?.[key]
    if (fromSettings) return fromSettings.trim()

    const fromEnv = config.public[envKey as keyof typeof config.public]
    if (typeof fromEnv === 'string' && fromEnv) return fromEnv.trim()

    return fallback
  }

  return { get }
}
