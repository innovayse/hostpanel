<script setup lang="ts">
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import ReportComingSoon from '../components/ReportComingSoon.vue'
</script>

<template>
  <ReportPage title="Client">
    <ReportComingSoon />
    <ReportTimestamp />
</ReportPage>
</template>
