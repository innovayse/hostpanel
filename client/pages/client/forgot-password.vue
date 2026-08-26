<template>
  <div class="min-h-dvh bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute inset-0" style="background: radial-gradient(circle at center, transparent 40%, #0a0a0f 100%)" />
    </div>

    <div class="w-full max-w-md relative z-10">
      <div class="text-center mb-8">
        <h1 class="text-2xl font-bold text-white">Reset password</h1>
        <p class="text-gray-400 text-sm mt-1">We'll email you a link to reset your password</p>
      </div>

      <div class="p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10">
        <div v-if="success" class="text-center text-green-400 text-sm">
          If an account exists for that email, a reset link has been sent.
        </div>
        <form v-else class="space-y-4" @submit.prevent="handleForgot">
          <div>
            <label class="block text-xs font-medium text-gray-400 uppercase tracking-wide mb-1.5">Email</label>
            <input v-model="email" type="email" required placeholder="you@example.com" class="w-full bg-white/5 border border-white/10 focus:border-primary-500 text-white placeholder-gray-500 rounded-lg px-3 py-2.5 text-sm outline-none transition-all" />
          </div>
          <button type="submit" :disabled="loading" class="w-full py-3 rounded-xl bg-gradient-to-r from-primary-600 to-primary-500 text-white font-semibold disabled:opacity-50">
            {{ loading ? 'Sending...' : 'Send reset link' }}
          </button>
        </form>
      </div>

      <p class="text-center text-sm mt-6">
        <NuxtLink to="/client/login" class="text-primary-400 hover:text-primary-300 transition-colors">Back to sign in</NuxtLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const email = ref('')
const success = ref(false)
const loading = ref(false)

async function handleForgot() {
  loading.value = true
  try {
    await apiFetch('/api/portal/auth/forgot-password', { method: 'POST', body: { email: email.value } })
  } catch {}
  success.value = true
  loading.value = false
}
</script>
