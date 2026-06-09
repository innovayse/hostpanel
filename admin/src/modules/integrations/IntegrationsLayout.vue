<script setup lang="ts">
import IntegrationsInnerSidebar from './components/IntegrationsInnerSidebar.vue'
</script>

<template>
  <div class="flex h-full min-h-0 flex-1">
    <IntegrationsInnerSidebar />
    <div class="flex-1 overflow-auto">
      <RouterView />
    </div>
  </div>
</template>
