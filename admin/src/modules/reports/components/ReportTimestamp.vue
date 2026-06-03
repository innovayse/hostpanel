<script setup lang="ts">
/**
 * Displays "Report Generated on DD/MM/YYYY HH:MM" timestamp.
 * Captures the exact time when the report was loaded (not reactive).
 */
import { ref, onMounted } from 'vue'

const timestamp = ref('')

onMounted(() => {
  const now = new Date()
  const dd = String(now.getDate()).padStart(2, '0')
  const mm = String(now.getMonth() + 1).padStart(2, '0')
  const yyyy = now.getFullYear()
  const hh = String(now.getHours()).padStart(2, '0')
  const min = String(now.getMinutes()).padStart(2, '0')
  timestamp.value = `${dd}/${mm}/${yyyy} ${hh}:${min}`
})
</script>

<template>
  <div class="text-[0.68rem] text-text-muted mt-4 text-right">
    Report Generated on {{ timestamp }}
  </div>
</template>
