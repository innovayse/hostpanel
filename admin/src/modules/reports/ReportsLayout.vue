<script setup lang="ts">
import ReportsInnerSidebar from './components/ReportsInnerSidebar.vue'
</script>

<template>
  <div class="flex h-full min-h-0 flex-1">
    <ReportsInnerSidebar />
    <div class="flex-1 overflow-auto">
      <RouterView />
    </div>
  </div>
</template>
