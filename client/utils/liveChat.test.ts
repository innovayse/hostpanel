import { describe, expect, it, vi } from 'vitest'
import {
  LIVE_CHAT_READY_EVENT,
  buildLiveChatLoader,
  closeLiveChat,
  openLiveChat,
  setLiveChatLocale,
} from './liveChat'

/**
 * Stands in for the Innochat SDK bundle.
 *
 * Modelled on what the real bundle at the chat host actually does: it defines
 * `window.innochatSDK`, and `run()` starts the widget, which some time later
 * defines `window.$innochat` and fires `innochat:ready`. Nothing here knows
 * about Chatwoot, which is the point — the bundle this application loads does
 * not either.
 *
 * The delay is why `fireReady` is separate rather than being called from inside
 * `run`: the loader registers its listener after calling `run`, and a stub that
 * fired the event synchronously would fail a loader that is correct.
 *
 * @param win The fake window the loader is run against.
 * @returns The widget the SDK creates, and a trigger for its ready event.
 */
const installSdk = (win: Record<string, any>) => {
  const widget = {
    toggle: vi.fn(),
    setLocale: vi.fn(),
    setCustomAttributes: vi.fn(),
  }

  win.innochatSDK = {
    run: vi.fn((options: { websiteToken: string, baseUrl: string }) => {
      win.$innochat = widget
      return options
    }),
  }

  const fireReady = () => {
    for (const listener of win.__listeners[LIVE_CHAT_READY_EVENT] ?? []) listener()
  }

  return { widget, fireReady }
}

/**
 * Runs a loader script the way a browser would.
 *
 * The script is executed with `window` and `document` as parameters, so the
 * free variables it references resolve to these stubs. The stub document
 * records the script element the loader inserts, which is what lets the test
 * fire its `onload` at the right moment.
 *
 * @param source Loader source from {@link buildLiveChatLoader}.
 */
const runLoader = (source: string) => {
  const inserted: any[] = []
  const firstScript = { parentNode: { insertBefore: (node: any) => inserted.push(node) } }

  const win: Record<string, any> = {
    __listeners: {} as Record<string, Array<() => void>>,
    addEventListener(event: string, listener: () => void) {
      (this.__listeners[event] ??= []).push(listener)
    },
  }

  const doc = {
    createElement: () => ({}) as Record<string, any>,
    getElementsByTagName: () => [firstScript],
  }

  // eslint-disable-next-line no-new-func -- executing the built source is the point of the test
  new Function('window', 'document', source)(win, doc)

  return { win, script: inserted[0] as Record<string, any> }
}

const OPTIONS = {
  baseUrl: 'https://chat.example.com',
  websiteToken: 'test-token',
  locale: 'hy',
  language: 'Armenian',
}

describe('buildLiveChatLoader', () => {
  it('fetches the SDK from the configured chat host', () => {
    const { script } = runLoader(buildLiveChatLoader(OPTIONS))

    expect(script.src).toBe('https://chat.example.com/packs/js/sdk.js')
    expect(script.async).toBe(true)
  })

  it('starts the widget once the SDK bundle has loaded', () => {
    const { win, script } = runLoader(buildLiveChatLoader(OPTIONS))
    const { widget } = installSdk(win)

    // Nothing has started the widget yet: the bundle has only just arrived.
    expect(win.$innochat).toBeUndefined()

    script.onload()

    // This is the assertion the old loader failed. It looked for a Chatwoot
    // global the Innochat bundle never defines, returned early, and left the
    // page with a chat button wired to an API that was never created.
    expect(win.innochatSDK.run).toHaveBeenCalledWith({
      websiteToken: 'test-token',
      baseUrl: 'https://chat.example.com',
    })
    expect(win.$innochat).toBe(widget)
  })

  it('hands the widget the visitor\'s language once it is ready', () => {
    const { win, script } = runLoader(buildLiveChatLoader(OPTIONS))
    const { widget, fireReady } = installSdk(win)

    script.onload()
    fireReady()

    expect(widget.setLocale).toHaveBeenCalledWith('hy')
    expect(widget.setCustomAttributes).toHaveBeenCalledWith({ language: 'Armenian' })
  })

  it('does nothing when the bundle fails to define its SDK', () => {
    const { win, script } = runLoader(buildLiveChatLoader(OPTIONS))

    expect(() => script.onload()).not.toThrow()
    expect(win.$innochat).toBeUndefined()
  })
})

describe('openLiveChat', () => {
  it('opens the running widget and reports that it did', () => {
    const win: Record<string, any> = {}
    const widget = { toggle: vi.fn(), setLocale: vi.fn(), setCustomAttributes: vi.fn() }
    win.$innochat = widget

    expect(openLiveChat(win)).toBe(true)
    expect(widget.toggle).toHaveBeenCalledWith('open')
  })

  it('reports failure rather than pretending, when the widget never started', () => {
    // The whole bug in one line: the click handler used to reach for a global
    // that was not there and say nothing about it.
    expect(openLiveChat({})).toBe(false)
  })

  it('survives being called during server rendering, where there is no window', () => {
    expect(openLiveChat(undefined)).toBe(false)
  })
})

describe('closeLiveChat', () => {
  it('closes the running widget', () => {
    const widget = { toggle: vi.fn(), setLocale: vi.fn(), setCustomAttributes: vi.fn() }

    expect(closeLiveChat({ $innochat: widget })).toBe(true)
    expect(widget.toggle).toHaveBeenCalledWith('close')
  })

  it('reports failure when there is no widget', () => {
    expect(closeLiveChat({})).toBe(false)
  })
})

describe('setLiveChatLocale', () => {
  it('passes the locale and the language on to a running widget', () => {
    const widget = { toggle: vi.fn(), setLocale: vi.fn(), setCustomAttributes: vi.fn() }

    expect(setLiveChatLocale({ $innochat: widget }, 'ru', 'Russian')).toBe(true)
    expect(widget.setLocale).toHaveBeenCalledWith('ru')
    expect(widget.setCustomAttributes).toHaveBeenCalledWith({ language: 'Russian' })
  })

  it('reports failure when there is no widget to tell', () => {
    expect(setLiveChatLocale({}, 'ru', 'Russian')).toBe(false)
  })
})
