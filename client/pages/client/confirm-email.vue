<template>
  <div class="min-h-dvh bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute inset-0" style="background: radial-gradient(circle at center, transparent 40%, #0a0a0f 100%)" />
    </div>

    <div class="w-full max-w-md relative z-10">
      <div class="p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 text-center">
        <div v-if="status === 'loading'" class="text-gray-400 text-sm">Confirming your email...</div>
        <div v-else-if="status === 'success'">
          <div class="text-green-400 text-sm mb-4">Email confirmed! You can now sign in.</div>
          <NuxtLink to="/client/login" class="text-primary-400 hover:text-primary-300 transition-colors font-medium">Sign in</NuxtLink>
        </div>
        <div v-else>
          <div class="text-red-400 text-sm mb-4">{{ message }}</div>
          <NuxtLink to="/client/register" class="text-primary-400 hover:text-primary-300 transition-colors font-medium">Back to register</NuxtLink>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })
const route = useRoute()

const status = ref<'loading' | 'success' | 'error'>('loading')
const message = ref('')

onMounted(async () => {
  const email = route.query.email as string
  const token = route.query.token as string
  if (!email || !token) {
    status.value = 'error'
    message.value = 'Invalid confirmation link.'
    return
  }
  try {
    await apiFetch('/api/portal/auth/confirm-email', { method: 'POST', body: { email, token } })
    status.value = 'success'
  } catch {
    status.value = 'error'
    message.value = 'Confirmation failed. The link may have expired.'
  }
})
</script>
