<template>
  <div
    data-testid="ui-glass-panel"
    class="ui-glass rounded-[2rem] sm:rounded-[3rem] bg-black/30 backdrop-blur-xl p-6 sm:p-10 md:p-14 lg:p-16 border border-white/10 shadow-drop-glass"
  >
    <slot />
  </div>
</template>

<script setup lang="ts">
/**
 * UiGlassPanel: a large frosted-glass panel for copy that sits on a photograph.
 *
 * It is dark whatever the colour mode (the photograph under it is), so put it inside a `dark`
 * scope when its text should be light. Padding is generous and baked in; width and position are
 * the caller's.
 */
</script>
