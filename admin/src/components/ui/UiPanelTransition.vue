<script setup lang="ts">
/**
 * Shared open/close animation of the kit's dropdown panels (select lists, row-action menus,
 * pickers): a short fade with a slight scale and lift, 300 ms in and 300 ms out. One component
 * keeps every panel moving the same way; it is skipped when the user asks for reduced motion.
 * Wrap the panel (the element with `v-if`) in it.
 */
</script>

<template>
  <Transition
    enter-active-class="transition duration-300 ease-out motion-reduce:transition-none"
    enter-from-class="opacity-0 scale-95 -translate-y-1"
    enter-to-class="opacity-100 scale-100 translate-y-0"
    leave-active-class="transition duration-300 ease-in motion-reduce:transition-none"
    leave-from-class="opacity-100 scale-100 translate-y-0"
    leave-to-class="opacity-0 scale-95 -translate-y-1"
  >
    <slot />
  </Transition>
</template>
