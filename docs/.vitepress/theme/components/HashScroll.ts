/**
 * Draws nothing. When the page is opened with a `#hash`, it scrolls to that heading again once the page
 * is mounted, once the fonts are ready and once the page has loaded, because the layout moves at each of
 * those moments (see `hashScroll.ts`). Any scroll the reader makes themselves ends it. Rendered in the
 * `layout-bottom` slot of the theme.
 */
import { defineComponent, onBeforeUnmount, onMounted } from 'vue'
import { createHashSettler, scrollToHash } from '../hashScroll'

/** Input that means the reader has taken over the scrolling. */
const USER_INPUT_EVENTS = ['wheel', 'touchstart', 'keydown', 'pointerdown'] as const

/** How long after mounting the page may still be moved back to the heading, in milliseconds. */
const SETTLE_WINDOW_MS = 10000

export default defineComponent({
  name: 'HashScroll',
  setup() {
    /** Removes every listener and timer; set while a settle is running. */
    let stop: (() => void) | null = null

    onMounted(() => {
      if (!window.location.hash) {
        return
      }
      const settler = createHashSettler(() => scrollToHash(document, window.location.hash))
      /** Scrolls to the hash target once more. */
      const run = (): void => {
        settler.run()
      }
      /** Ends the settle: stops the run and drops every listener and the timer. */
      const finish = (): void => {
        settler.cancel()
        USER_INPUT_EVENTS.forEach((name) => window.removeEventListener(name, finish))
        window.removeEventListener('load', run)
        window.clearTimeout(timer)
        stop = null
      }
      const timer = window.setTimeout(finish, SETTLE_WINDOW_MS)
      stop = finish
      USER_INPUT_EVENTS.forEach((name) => window.addEventListener(name, finish, { passive: true }))
      window.requestAnimationFrame(run)
      void document.fonts?.ready.then(run)
      if (document.readyState !== 'complete') {
        window.addEventListener('load', run, { once: true })
      }
    })

    onBeforeUnmount(() => stop?.())

    return () => null
  },
})
