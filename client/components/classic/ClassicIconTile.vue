<template>
  <UiStack align="center" justify="center" gap="none" class="h-11 w-11 shrink-0 rounded-md bg-classic-brand-soft text-classic-brand" data-testid="classic-icon-tile">
    <Icon :name="icon" class="text-2xl" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * ClassicIconTile. A flat square icon mark for the classic template: the soft brand tint with
 * the icon in the solid brand colour. No border, no glow, no per-item colour.
 */

/** Props of ClassicIconTile. */
interface Props {
  /** Nuxt Icon name, e.g. `mdi:lock-check`. */
  icon: string
}

defineProps<Props>()
</script>
