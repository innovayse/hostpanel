<template>
  <iframe
    ref="frameRef"
    :srcdoc="html"
    :title="title"
    sandbox="allow-same-origin"
    class="w-full rounded-2xl bg-white border-0 min-h-[500px] h-[var(--frame-height,500px)]"
    data-testid="email-frame"
    @load="fitToContent"
  />
</template>

<script setup lang="ts">
/**
 * EmailFrame: shows an email's HTML body in a sandboxed frame.
 *
 * The body is untrusted markup, so it never enters the page's own DOM: it is the `srcdoc` of an
 * iframe whose sandbox grants only same-origin (so the height can be measured) and no scripts.
 * Once loaded the frame grows to its content; the height travels as the CSS custom property
 * `--frame-height`, written from script, so the template carries no inline `style`.
 */
import { ref } from 'vue'

/** Props of EmailFrame. */
interface Props {
  /** The HTML body of the message, exactly as the API sent it. */
  html: string
  /** Accessible name of the frame. */
  title: string
}

defineProps<Props>()

/** Space added below the measured content so nothing is clipped, in pixels. */
const FRAME_PADDING = 32

/** The iframe element. */
const frameRef = ref<HTMLIFrameElement | null>(null)

/** Grows the frame to the height of the document it loaded. */
const fitToContent = (): void => {
  const frame = frameRef.value
  const body = frame?.contentDocument?.body
  if (!frame || !body) {
    return
  }
  frame.style.setProperty('--frame-height', `${body.scrollHeight + FRAME_PADDING}px`)
}
</script>
