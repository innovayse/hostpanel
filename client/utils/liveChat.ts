/**
 * Live chat widget integration.
 *
 * The provider is Innochat, self-hosted at the operator's own chat host. It is
 * built from Chatwoot and keeps Chatwoot's DOM class names — `.woot-widget-holder`
 * is still what the widget mounts into — but its SDK is rebranded all the way
 * down and exposes none of Chatwoot's globals. Everything the page talks to is
 * named here rather than spelled out at each call site, because the two sets
 * differ by one word and the wrong one fails silently: `window.$chatwoot?.toggle()`
 * on a page running Innochat is a no-op with no error, which is exactly how this
 * went unnoticed.
 */

/** Global the SDK reads its configuration from, before it runs. */
export const LIVE_CHAT_SETTINGS_GLOBAL = 'innochatSettings'

/** Global the loaded SDK bundle defines. Carries `run()`. */
export const LIVE_CHAT_SDK_GLOBAL = 'innochatSDK'

/** Global the SDK creates once `run()` has started the widget. Carries the API. */
export const LIVE_CHAT_API_GLOBAL = '$innochat'

/** Event the SDK fires on `window` when the widget is ready to be called. */
export const LIVE_CHAT_READY_EVENT = 'innochat:ready'

/** Class the widget's container carries — inherited from Chatwoot, still emitted. */
export const LIVE_CHAT_HOLDER_SELECTOR = '.woot-widget-holder'

/** Path of the SDK bundle under the chat host. */
export const LIVE_CHAT_SDK_PATH = '/packs/js/sdk.js'

/** The subset of the widget API this application calls. */
export interface LiveChatApi {
  toggle: (state: 'open' | 'close') => void
  setLocale: (locale: string) => void
  setCustomAttributes: (attributes: Record<string, string>) => void
}

/** A window that may or may not have the widget running on it yet. */
export interface LiveChatWindow {
  [LIVE_CHAT_API_GLOBAL]?: LiveChatApi
}

/**
 * Reads the widget API off a window, if the SDK has started it.
 *
 * The parameter is `unknown` rather than `LiveChatWindow` because the real
 * argument is always the browser's own `Window`, which TypeScript will not
 * accept for a type whose properties are all optional and none of which it
 * declares. Narrowing here keeps the assertion in one place instead of at every
 * call site, and the check below is a real one: the SDK adds this property at
 * runtime, so nothing can prove it is there.
 *
 * @param win Window to look at; `undefined` during server rendering.
 * @returns The API, or null when the widget is not running.
 */
const api = (win: unknown): LiveChatApi | null => {
  const widget = (win as LiveChatWindow | undefined)?.[LIVE_CHAT_API_GLOBAL]

  return typeof widget?.toggle === 'function' ? widget : null
}

/**
 * Opens the widget.
 *
 * @param win The browser window.
 * @returns Whether the widget was there to open. A false is worth acting on —
 *   it means the visitor clicked a control that did nothing.
 */
export const openLiveChat = (win: unknown): boolean => {
  const widget = api(win)
  if (!widget) return false

  widget.toggle('open')
  return true
}

/**
 * Closes the widget.
 *
 * @param win The browser window.
 * @returns Whether the widget was there to close.
 */
export const closeLiveChat = (win: unknown): boolean => {
  const widget = api(win)
  if (!widget) return false

  widget.toggle('close')
  return true
}

/**
 * Tells a running widget which language the visitor is reading the site in.
 *
 * @param win The browser window.
 * @param locale Locale code the widget understands, e.g. `hy`.
 * @param language Language name passed through as a conversation attribute, so
 *   an agent picking the conversation up knows which language to answer in.
 * @returns Whether the widget was running to be told.
 */
export const setLiveChatLocale = (
  win: unknown,
  locale: string,
  language: string,
): boolean => {
  const widget = api(win)
  if (!widget) return false

  widget.setLocale(locale)
  widget.setCustomAttributes({ language })
  return true
}

/** Everything the loader needs to start the widget. */
export interface LiveChatLoaderOptions {
  /** Origin of the chat host, without a trailing slash. */
  baseUrl: string
  /** Website token identifying this site to the chat host. */
  websiteToken: string
  /** Locale code to hand the widget once it is ready. */
  locale: string
  /** Language name recorded on the conversation. */
  language: string
}

/**
 * Builds the inline script that loads the SDK and starts the widget.
 *
 * Returned as source rather than executed here because it is injected through
 * `useHead` at `bodyClose`, so the fetch starts from the server-rendered HTML
 * instead of waiting for hydration. Building it in a function is what lets the
 * globals it names be asserted in a test — the previous version was a template
 * literal inside `app.vue`, unreachable from anything that could have caught
 * the names being wrong.
 *
 * The message bubble is suppressed: the floating contact button is the only way
 * into the widget, so a second launcher would sit on top of it.
 *
 * @param options Chat host, token and language for this render.
 * @returns JavaScript source, ready to inline.
 */
export const buildLiveChatLoader = ({
  baseUrl,
  websiteToken,
  locale,
  language,
}: LiveChatLoaderOptions): string => `
window.${LIVE_CHAT_SETTINGS_GLOBAL} = {"position":"right","type":"standard","launcherTitle":"","hideMessageBubble":true};
(function(d,t){
  var BASE_URL=${JSON.stringify(baseUrl)};
  var g=d.createElement(t),s=d.getElementsByTagName(t)[0];
  g.src=BASE_URL+${JSON.stringify(LIVE_CHAT_SDK_PATH)};
  g.async=true;
  s.parentNode.insertBefore(g,s);
  g.onload=function(){
    if (!window.${LIVE_CHAT_SDK_GLOBAL}) return;
    window.${LIVE_CHAT_SDK_GLOBAL}.run({
      websiteToken: ${JSON.stringify(websiteToken)},
      baseUrl: BASE_URL
    });
    window.addEventListener(${JSON.stringify(LIVE_CHAT_READY_EVENT)}, function(){
      window.${LIVE_CHAT_API_GLOBAL}.setLocale(${JSON.stringify(locale)});
      window.${LIVE_CHAT_API_GLOBAL}.setCustomAttributes({ language: ${JSON.stringify(language)} });
    });
  }
})(document,"script");`
