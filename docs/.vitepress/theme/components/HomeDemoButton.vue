<script setup lang="ts">
import { useData } from 'vitepress'
import { computed } from 'vue'

const { theme } = useData()

const demoUrl = computed<string>(() => (theme.value as Record<string, string>).demoAdminUrl ?? '')
</script>

<template>
  <div v-if="demoUrl" class="home-demo-action">
    <a :href="demoUrl" target="_blank" rel="noopener noreferrer" class="VPButton medium alt">
      Try the demo
    </a>
  </div>
</template>

<style scoped>
.home-demo-action {
  display: flex;
  justify-content: center;
  margin-top: 12px;
}
</style>
