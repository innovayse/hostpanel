<script setup lang="ts">
/**
 * An icon of the Material Design set drawn by its name, for the places where the name itself is data an operator types
 * (the icon of a homepage slide, shown on the storefront by the same name).
 *
 * Every other icon of the panel is a stored shape drawn by `UiIcon`; this is the one place the named icon package is
 * imported, so a screen never imports it on its own.
 */
import { Icon as NamedIcon } from '@iconify/vue'
import { computed } from 'vue'

/** Props for UiNamedIcon. */
const props = defineProps<{
  /** Icon name in the set, without the set prefix (for example `cloud-check`). */
  name: string
}>()

/** Full icon id the package resolves: the set prefix and the name. */
const id = computed<string>(() => `mdi:${props.name}`)
</script>

<template>
  <NamedIcon :icon="id" aria-hidden="true" />
</template>
