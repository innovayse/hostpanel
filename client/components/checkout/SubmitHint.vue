<template>
  <!-- The live region is always in the DOM: a region inserted together with its text is not announced. -->
  <UiText
    as="p"
    size="xs"
    tone="muted"
    class="text-center"
    :class="message ? 'mt-3' : ''"
    aria-live="polite"
    data-testid="checkout-submit-hint"
  >
    {{ message }}
  </UiText>
</template>

<script setup lang="ts">
/**
 * SubmitHint: the small muted line under a busy submit button.
 *
 * The button itself only shows a spinner and a short label ("Redirecting"); anything longer, such
 * as why the page is about to change, goes here so it can never overflow or resize the button.
 * Both checkout templates use it, so the busy state reads the same in each.
 */

/** Props of SubmitHint. */
interface Props {
  /** The sentence to show and announce politely; empty hides the line without removing the live region. */
  message?: string
}

/** The props of the component. */
withDefaults(defineProps<Props>(), { message: '' })
</script>
