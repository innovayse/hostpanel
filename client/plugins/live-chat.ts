import type { Ref } from 'vue'
import {
  LIVE_CHAT_HOLDER_SELECTOR,
  buildLiveChatLoader,
  closeLiveChat,
  setLiveChatLocale,
} from '~/utils/liveChat'

/** Language name recorded on the conversation, so the person answering knows which to use. */
const languageOf = (code: string): string =>
  code === 'hy' ? 'Armenian' : code === 'ru' ? 'Russian' : 'English'

/**
 * Live-chat widget loader, driven entirely by settings.
 *
 * The chat server and its website tokens used to be literals in `app.vue` — the vendor's
 * own — so every self-hosted install opened a chat with the vendor. Now
 * `portal.chat.provider` is the switch, `portal.chat.base_url` names the server and
 * `portal.chat.website_token` (with `.ru` / `.hy` overrides) identifies the storefront;
 * with any of the three empty, no script is emitted.
 *
 * The loader is still injected through `useHead` at `bodyClose` so the SDK fetch starts
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
    const { locale } = nuxtApp.$i18n as { locale: Ref<string> }

    const enabled = computed(() => {
      const provider = get('portal.chat.provider', 'portalChatProvider').toLowerCase()
      return provider === 'chatwoot' || provider === 'innochat'
    })
    const baseUrl = computed(() => get('portal.chat.base_url', 'portalChatBaseUrl'))

    /** The token for the current locale, falling back to the default one. */
    const token = computed(() => {
      const perLocale = locale.value === 'ru'
        ? get('portal.chat.website_token.ru', 'portalChatWebsiteTokenRu')
        : locale.value === 'hy'
          ? get('portal.chat.website_token.hy', 'portalChatWebsiteTokenHy')
          : ''
      return perLocale || get('portal.chat.website_token', 'portalChatWebsiteToken')
    })

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
