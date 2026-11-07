<template>
  <iframe
    ref="frame"
    :src="src"
    :title="title"
    sandbox="allow-scripts allow-forms"
    referrerpolicy="no-referrer"
    allow=""
    loading="lazy"
    class="absolute inset-0 h-full w-full border-0 bg-white"
    :data-testid="testid"
  />
</template>

<script setup lang="ts">
/**
 * UiPreviewFrame - the one place an `<iframe>` of untrusted content is written, its attributes fixed.
 *
 * No `allow-same-origin`: the frame gets an opaque origin, so it can never reach the panel or drive a signed-in session
 * of the framed site. No `allow-top-navigation*`, `allow-popups*`, `allow-modals` or `allow-downloads` either: a preview
 * shows a site, it does not operate it. A caller checks the address first; a target refusing framing stays blank.
 *
 * A caller gets the frame's window for `postMessage` only: with an opaque origin nothing else of it can be read.
 *
 * It fills the nearest positioned box: a percentage height needs an ancestor with a definite height, which a flex item
 * that grows does not have, and an iframe left without one is drawn 150 pixels tall.
 */

/** Props of UiPreviewFrame. */
interface Props {
  /** The address, already checked by the caller. */
  src: string
  /** The frame's accessible name. */
  title: string
  /** `data-testid` of the frame. */
  testid?: string
}

withDefaults(defineProps<Props>(), {
  testid: undefined,
})

/** The frame element. */
const frame = ref<HTMLIFrameElement | null>(null)

/**
 * The window of the frame's content, the only thing a caller may compare or post to; the content itself stays out of reach.
 *
 * @returns The window, or null before the frame exists.
 */
const contentWindow = (): Window | null => frame.value?.contentWindow ?? null

defineExpose({ contentWindow })
</script>
