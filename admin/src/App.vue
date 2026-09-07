<script setup lang="ts">
/**
 * Root application component — renders the active route view.
 *
 * Also the one place the operator's branding is applied to the document. It runs here
 * rather than in a layout because the login screen has no layout: a white-labelled
 * deployment has to show its own mark on the very first page, before any session exists.
 */
import { useFavicon } from './composables/useFavicon'

useFavicon()
</script>

<template>
  <RouterView />
</template>
