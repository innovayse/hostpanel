<template>
  <div class="min-h-screen bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">

    <!-- ── Background (matches Hero section) ─────────────────────────────── -->
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <!-- Animated blobs -->
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-secondary-500/30 rounded-full blur-[120px] animate-blob animation-delay-2000" />
      <div class="absolute bottom-0 left-1/2 w-[400px] h-[400px] bg-cyan-500/20 rounded-full blur-[120px] animate-blob animation-delay-4000" />

      <!-- Grid pattern -->
      <div class="absolute inset-0 opacity-[0.02]">
        <div class="absolute inset-0" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" />
      </div>

      <!-- Radial vignette -->
      <div class="absolute inset-0" style="background: radial-gradient(circle at center, transparent 40%, #0a0a0f 100%)" />
    </div>

    <!-- Page corner accents -->
    <div class="absolute top-0 right-0 w-32 h-32 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-32 h-32 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />

    <!-- Floating particles -->
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute top-1/4 right-1/4 w-2 h-2 bg-primary-400 rounded-full animate-float" style="animation-delay: 0.5s;" />
      <div class="absolute top-1/3 left-1/3 w-3 h-3 bg-secondary-400 rounded-full animate-float" style="animation-delay: 1.5s;" />
      <div class="absolute bottom-1/3 right-1/3 w-2 h-2 bg-cyan-400 rounded-full animate-float" style="animation-delay: 2.5s;" />
    </div>

    <!-- ── Card ───────────────────────────────────────────────────────────── -->
    <div class="w-full max-w-md relative z-10">
      <!-- Logo -->
      <div class="text-center mb-8">
        <NuxtLink to="/" class="inline-block mb-6">
          <NuxtImg
            src="/logo.svg"
            alt="Innovayse"
            width="160"
            height="48"
            loading="eager"
            class="h-12 w-auto mx-auto"
          />
        </NuxtLink>
        <h1 class="text-2xl font-bold text-white">{{ $t('client.login.title') }}</h1>
        <p class="text-gray-400 text-sm mt-1">{{ $t('client.login.subtitle') }}</p>
      </div>

      <div class="relative p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 backdrop-blur-sm">
        <!-- Card corner accents -->
        <div class="absolute top-0 left-0 w-12 h-12 border-l-2 border-t-2 border-primary-500/40 rounded-tl-2xl pointer-events-none" />
        <div class="absolute bottom-0 right-0 w-12 h-12 border-r-2 border-b-2 border-cyan-500/40 rounded-br-2xl pointer-events-none" />

        <a
          href="/api/portal/auth/sso/authorize"
          class="flex items-center justify-center gap-3 w-full py-3 px-6 rounded-xl bg-gradient-to-r from-primary-600 to-primary-500 text-white font-semibold text-base hover:from-primary-500 hover:to-primary-400 transition-all duration-200 shadow-lg shadow-primary-500/25"
        >
          <svg width="20" height="20" viewBox="0 0 22 22" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M11 2L20 7V15L11 20L2 15V7L11 2Z" stroke="currentColor" stroke-width="1.5" fill="none"/>
            <path d="M11 7L16 10V14L11 17L6 14V10L11 7Z" fill="currentColor" opacity="0.7"/>
          </svg>
          {{ $t('client.login.continueWithInnovayse') }}
        </a>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })
</script>
