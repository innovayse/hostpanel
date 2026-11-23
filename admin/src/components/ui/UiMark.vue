<script setup lang="ts">
/**
 * The matched part of a text, drawn as a highlight: semibold in the accent colour on the surrounding background.
 * The kit's one `mark`, so a search that highlights what it found (the command palette) reads the same everywhere.
 */
</script>

<template>
  <mark class="bg-transparent font-semibold text-text-accent"><slot /></mark>
</template>
