/**
 * Wraps an ordered list with styled numbered circles.
 * Usage: place `<li>` elements as slot content.
 */
<script setup lang="ts">
</script>

<template>
  <ol class="steps">
    <slot />
  </ol>
</template>

<style>
/* Global so that slotted <li> picks up the counter styles */
.steps {
  list-style: none;
  padding: 0;
  margin: 20px 0;
  counter-reset: steps-counter;
}

.steps > li {
  counter-increment: steps-counter;
  display: flex;
  gap: 16px;
  align-items: flex-start;
  margin-bottom: 20px;
  line-height: 1.6;
}

.steps > li::before {
  content: counter(steps-counter);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background-color: var(--vp-c-brand-1);
  color: #fff;
  font-size: 0.8rem;
  font-weight: 700;
  margin-top: 1px;
}

.steps > li > * {
  margin: 0;
}
</style>
