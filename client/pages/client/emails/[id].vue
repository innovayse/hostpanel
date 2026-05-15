<template>
  <div>
    <NuxtLink
      to="/client/emails"
      class="inline-flex items-center gap-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white text-sm transition-colors mb-8"
    >
      <ArrowLeft :size="16" :stroke-width="2" />
      {{ $t('client.emails.backTo') }}
    </NuxtLink>

    <!-- Loading -->
    <div v-if="pending" class="space-y-4">
      <div class="h-8 w-2/3 rounded-lg bg-white/5 animate-pulse" />
      <div class="h-4 w-1/3 rounded-lg bg-white/5 animate-pulse" />
      <div class="h-96 rounded-2xl bg-white/5 animate-pulse mt-6" />
    </div>

    <!-- Not found -->
    <div v-else-if="error || !email" class="text-center py-20">
      <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
      <p class="text-gray-400">{{ $t('client.emails.notFound') }}</p>
    </div>

    <template v-else>
      <!-- Header -->
      <div class="mb-6">
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white mb-1">{{ email.subject }}</h1>
        <p class="text-sm text-gray-500">{{ email.date }}</p>
      </div>

      <!-- Message body in sandboxed iframe -->
      <UiCard padding="none">
        <iframe
          ref="iframeRef"
          sandbox="allow-same-origin"
          class="w-full rounded-2xl bg-white"
          style="min-height: 500px; border: none;"
          @load="resizeIframe"
        />
      </UiCard>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, AlertCircle } from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const route = useRoute()

const { data: email, pending, error } = await useApi<{
  id: string
  date: string
  subject: string
  message: string
}>(`/api/portal/client/emails/${route.params.id}`)

const iframeRef = ref<HTMLIFrameElement | null>(null)

function resizeIframe() {
  const iframe = iframeRef.value
  if (!iframe?.contentDocument) return
  iframe.style.height = iframe.contentDocument.body.scrollHeight + 32 + 'px'
}

// Write HTML message into iframe once loaded
watch([email, iframeRef], ([msg, iframe]) => {
  if (!msg?.message || !iframe) return
  nextTick(() => {
    const doc = iframe.contentDocument
    if (!doc) return
    doc.open()
    doc.write(msg.message)
    doc.close()
    resizeIframe()
  })
}, { immediate: true })
</script>
