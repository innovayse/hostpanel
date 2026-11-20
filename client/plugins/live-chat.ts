/**
 * Live-chat widget loader plugin, driven entirely by settings.
 *
 * @module plugins/live-chat
 */

import { PortalSettingKey } from '~/constants/portalSettingKey'
import type { Ref } from 'vue'
import { ChatProvider } from '~/types/chatprovider'
import { AppLocale } from '~/types/applocale'
import {
  LIVE_CHAT_HOLDER_SELECTOR,
  buildLiveChatLoader,
  closeLiveChat,
  setLiveChatLocale,
} from '~/utils/liveChat'

/** The slice of the module's i18n instance this plugin reads. */
interface I18nLocale {
  /** The active locale code. */
  locale: Ref<string>
}

/** English name of each portal language, as recorded on a conversation. */
const LANGUAGE_NAMES: Record<AppLocale, string> = {
  [AppLocale.En]: 'English',
  [AppLocale.Ru]: 'Russian',
  [AppLocale.Hy]: 'Armenian',
  [AppLocale.Es]: 'Spanish',
}

/**
 * Language name recorded on the conversation, so the person answering knows which to use.
 *
 * @param code Active locale code.
 * @returns The English name of the language; English for any unknown code.
 */
const languageOf = (code: string): string =>
  (LANGUAGE_NAMES as Partial<Record<string, string>>)[code] ?? LANGUAGE_NAMES[AppLocale.En]

/**
 * Live-chat widget loader, driven entirely by settings.
 *
 * Nothing is hard-coded, so a self-hosted install opens a chat only with its own server.
 * `portal.chat.provider` is the switch, `portal.chat.base_url` names the server and
 * `portal.chat.website_token` (with `.ru` / `.hy` overrides) identifies the storefront;
 * with any of the three empty, no script is emitted.
 *
 * The loader is injected through `useHead` at `bodyClose` so the SDK fetch starts
 * from the server-rendered HTML rather than after hydration. It does not wait for cookie
 * consent: the widget is how a visitor reaches a person, not a tracker, and it opens only
 * when they click it.
 */
export default defineNuxtPlugin({
  name: 'live-chat',
  dependsOn: ['portal-settings'],
  setup(nuxtApp) {
    const { get } = usePortalSettings()
    // The module's own instance: useI18n() needs a component setup context, which a
    // plugin does not have. Typed by hand because the object-form plugin does not narrow
    // `$i18n` the way the destructured `({ $i18n })` signature in plugins/i18n.ts does.
    const { locale } = nuxtApp.$i18n as I18nLocale

    /** True when the operator selected a supported chat provider. */
    const enabled = computed(() => {
      const provider = get(PortalSettingKey.ChatProvider, 'portalChatProvider').toLowerCase()
      return provider === ChatProvider.LegacyChat || provider === ChatProvider.Innochat
    })
    /** The chat server's base URL. */
    const baseUrl = computed(() => get(PortalSettingKey.ChatBaseUrl, 'portalChatBaseUrl'))

    /** The token for the current locale, falling back to the default one. */
    const token = computed(() => {
      const perLocale = locale.value === AppLocale.Ru
        ? get(PortalSettingKey.ChatWebsiteTokenRu, 'portalChatWebsiteTokenRu')
        : locale.value === AppLocale.Hy
          ? get(PortalSettingKey.ChatWebsiteTokenHy, 'portalChatWebsiteTokenHy')
          : ''
      return perLocale || get(PortalSettingKey.ChatWebsiteToken, 'portalChatWebsiteToken')
    })

    /** True when provider, server and token are all set. */
    const configured = computed(() => enabled.value && Boolean(baseUrl.value) && Boolean(token.value))

    useHead({
      script: () => configured.value
        ? [{
            innerHTML: buildLiveChatLoader({
              baseUrl: baseUrl.value,
              websiteToken: token.value,
              locale: locale.value,
              language: languageOf(locale.value),
            }),
            type: 'text/javascript',
            tagPosition: 'bodyClose',
          }]
        : [],
    })

    if (import.meta.client) {
      // Tell a running widget about a locale change. One that is not running is not an
      // error: the visitor may simply not have opened it.
      watch(locale, (next) => {
        setLiveChatLocale(window, next, languageOf(next))
      })

      // Close the widget when the visitor clicks anywhere outside it.
      document.addEventListener('click', (e: MouseEvent) => {
        const widget = document.querySelector(LIVE_CHAT_HOLDER_SELECTOR)
        if (widget && !widget.contains(e.target as Node)) {
          closeLiveChat(window)
        }
      })
    }
  },
})
