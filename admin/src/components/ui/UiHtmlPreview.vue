<script setup lang="ts">
/**
 * Shows a piece of HTML exactly as a mail reader would draw it, in a frame that runs nothing.
 *
 * The markup goes into the frame's `srcdoc` and the frame carries an empty `sandbox`, so the page
 * inside cannot run scripts, submit forms, open windows or reach the panel's cookies or DOM,
 * however it was written. The frame has a white paper background, because a mail is drawn on one
 * whatever the panel's theme is. `data-testid` and classes land on the frame.
 */

/** Props for UiHtmlPreview. */
defineProps<{
  /** The HTML document to draw. */
  html: string
  /** Accessible name of the frame (already translated). */
  title: string
}>()
</script>

<template>
  <iframe
    :srcdoc="html"
    :title="title"
    sandbox=""
    class="h-[28rem] w-full rounded-lg border border-border bg-white"
  />
</template>
