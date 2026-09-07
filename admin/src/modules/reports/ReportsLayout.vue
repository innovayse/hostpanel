<script setup lang="ts">
import ReportsInnerSidebar from './components/ReportsInnerSidebar.vue'
</script>

<template>
  <!-- Bleeds past Layout's gutter so the inner sidebar meets the shell edge. -->
  <div class="flex h-full min-h-0 flex-1 -m-4 sm:-m-6 lg:-m-8">
    <ReportsInnerSidebar />
    <div class="flex-1 min-h-0 overflow-auto p-4 sm:p-6 lg:p-8">
      <RouterView />
    </div>
  </div>
</template>
