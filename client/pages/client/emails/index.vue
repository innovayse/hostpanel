<script setup lang="ts">
definePageMeta({ layout: 'client', middleware: 'client-auth' })
await navigateTo('/client/account?tab=emails', { replace: true })
</script>
