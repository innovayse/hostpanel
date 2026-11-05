<template>
  <iframe
    :src="src"
    :title="title"
    sandbox="allow-scripts allow-forms"
    referrerpolicy="no-referrer"
    allow=""
    loading="lazy"
    class="h-full w-full border-0 bg-white"
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
</script>
