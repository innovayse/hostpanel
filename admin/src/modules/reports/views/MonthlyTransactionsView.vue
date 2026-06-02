<script setup lang="ts">
import ReportPage from '../components/ReportPage.vue'
import ReportComingSoon from '../components/ReportComingSoon.vue'
</script>

<template>
  <ReportPage title="Monthly Transactions" description="All transactions grouped by month">
    <ReportComingSoon />
  </ReportPage>
</template>
