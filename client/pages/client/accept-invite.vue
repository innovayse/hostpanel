<template>
  <div class="min-h-dvh bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">

    <!-- ── Background (matches login/register pages) ──────────────────────── -->
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-secondary-500/30 rounded-full blur-[120px] animate-blob animation-delay-2000" />
      <div class="absolute bottom-0 left-1/2 w-[400px] h-[400px] bg-cyan-500/20 rounded-full blur-[120px] animate-blob animation-delay-4000" />

      <div class="absolute inset-0 opacity-[0.02]">
        <div class="absolute inset-0" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" />
      </div>

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
        <h1 class="text-2xl font-bold text-white">{{ $t('client.acceptInvite.title') }}</h1>
        <p class="text-gray-400 text-sm mt-1">{{ $t('client.acceptInvite.subtitle') }}</p>
      </div>

      <div class="relative p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 backdrop-blur-sm">
        <!-- Card corner accents -->
        <div class="absolute top-0 left-0 w-12 h-12 border-l-2 border-t-2 border-primary-500/40 rounded-tl-2xl pointer-events-none" />
        <div class="absolute bottom-0 right-0 w-12 h-12 border-r-2 border-b-2 border-cyan-500/40 rounded-br-2xl pointer-events-none" />

        <!-- Invalid token state -->
        <div v-if="invalidToken" class="text-center py-4">
          <div class="w-16 h-16 rounded-2xl bg-red-500/10 border border-red-500/20 flex items-center justify-center mx-auto mb-5">
            <AlertCircle :size="32" :stroke-width="1.5" class="text-red-400" />
          </div>
          <h2 class="text-lg font-bold text-white mb-2">{{ $t('client.acceptInvite.invalidTitle') }}</h2>
          <p class="text-gray-400 text-sm leading-relaxed">{{ $t('client.acceptInvite.invalidMessage') }}</p>
          <NuxtLink
            to="/client/login"
            class="inline-flex items-center gap-2 mt-6 text-sm text-primary-400 hover:text-primary-300 font-medium transition-colors"
          >
            <ArrowLeft :size="14" :stroke-width="2" />
            {{ $t('client.acceptInvite.backToSignIn') }}
          </NuxtLink>
        </div>

        <!-- Sign-in-required state — the invitation is fine, the visitor just has no session -->
        <div v-else-if="signInRequired" class="text-center py-4">
          <div class="w-16 h-16 rounded-2xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center mx-auto mb-5">
            <LogIn :size="32" :stroke-width="1.5" class="text-primary-400" />
          </div>
          <h2 class="text-lg font-bold text-white mb-2">{{ $t('client.acceptInvite.signInRequiredTitle') }}</h2>
          <p class="text-gray-400 text-sm leading-relaxed">{{ error }}</p>
          <UiButton
            variant="primary"
            size="lg"
            :full-width="true"
            class="mt-6 hover:shadow-xl hover:shadow-primary-500/30"
            @click="goToSignIn"
          >
            <LogIn :size="18" :stroke-width="2" class="mr-2" />
            {{ $t('client.acceptInvite.signInAction') }}
          </UiButton>
        </div>

        <!-- Form state -->
        <UiForm v-else :error="error" spacing="lg" @submit="handleSubmit">
          <p class="text-gray-400 text-sm leading-relaxed">{{ $t('client.acceptInvite.description') }}</p>
          <UiButton
            type="submit"
            variant="primary"
            size="lg"
            :full-width="true"
            :loading="loading"
            class="hover:shadow-xl hover:shadow-primary-500/30"
          >
            <UserPlus v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
            {{ loading ? $t('client.acceptInvite.submitting') : $t('client.acceptInvite.submit') }}
          </UiButton>
          <div class="text-center">
            <NuxtLink
              to="/client/login"
              class="inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-300 transition-colors"
            >
              <ArrowLeft :size="14" :stroke-width="2" />
              {{ $t('client.acceptInvite.backToSignIn') }}
            </NuxtLink>
          </div>
        </UiForm>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Accept invitation page — links the signed-in account to the client that invited it.
 *
 * Reads the invitation token out of the link's `?token=` query and posts it to
 * `/api/portal/auth/accept-invite`. On success the person is a user of that client account
 * and lands on the dashboard.
 *
 * **It no longer asks for a password.** It used to, and the password had nowhere to go: the
 * backend command carries a token only and resolves who accepted from the credential, so
 * whoever accepts must already be signed in. A form that collected a password and a
 * confirmation, then posted them to an endpoint that reads neither, would have reported a
 * password set that nothing ever stored. The password fields are replaced by the sign-in
 * step that flow actually needs.
 */
import { AlertCircle, ArrowLeft, LogIn, UserPlus } from 'lucide-vue-next'
import { useAuthApi } from '~/composables/apis/useAuthApi'
import { PortalErrorCode, apiErrorCode, apiErrorMessage } from '~/utils/apiError'

definePageMeta({ layout: false })

const route = useRoute()
const { t } = useI18n()
const config = useRuntimeConfig()
const { acceptInvite } = useAuthApi()

/** Invitation token extracted from URL query. */
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))

/** True when no token is present in the URL — the one check made before any request. */
const invalidToken = computed(() => !token.value)

/** True while the accept-invite request is in flight. */
const loading = ref(false)

/** Error message to display, empty when no error. Worded by the API, never composed here. */
const error = ref('')

/**
 * True when the last refusal was `INVITE_SIGN_IN_REQUIRED`, so the page shows the sign-in
 * step instead of the accept button. Every failure carries its recovery action; this is that
 * failure's.
 */
const signInRequired = ref(false)

/**
 * Sends the visitor to sign in and back to this exact URL, token and all.
 *
 * Mirrors what `middleware/client-auth.ts` does for the rest of the client area, because
 * this page is deliberately outside that middleware — the invitation link has to render its
 * own explanation rather than bounce a stranger straight into an identity provider.
 *
 * @returns The navigation, so a caller can await it.
 */
function goToSignIn(): ReturnType<typeof navigateTo> {
  const returnTo = encodeURIComponent(route.fullPath)

  if (config.public.authMode === 'sso') {
    const baseUrl = (config.public.baseUrl as string) || window.location.origin
    return navigateTo(`${baseUrl}/api/portal/auth/sso/authorize?returnTo=${returnTo}`, { external: true })
  }

  return navigateTo(`/client/login?redirect=${returnTo}`)
}

/**
 * Posts the invitation token and reports what the API said.
 *
 * On success it leaves with a full page load rather than a router push. Accepting grants the
 * Client role and links the account to a client that the running SPA has already decided it
 * does not have — `stores/client.ts` caches that answer for the session — so a router push
 * would land on a dashboard still rendering the no-profile notice.
 */
async function handleSubmit(): Promise<void> {
  loading.value = true
  error.value = ''
  signInRequired.value = false

  try {
    await acceptInvite(token.value)

    // Full page reload so the new role and cookies are picked up by middleware
    window.location.href = '/client/dashboard'
  } catch (err: unknown) {
    const code = apiErrorCode(err)

    // A code only earns a branch when the page must *do* something different for it. This
    // one swaps the whole panel for the sign-in step; expired and already-accepted keep the
    // backend's own sentence, which is the only place that knows why it refused.
    signInRequired.value = code === PortalErrorCode.InviteSignInRequired

    // The one sentence this frontend still words itself, and only because the backend never
    // sees the request it answers: `server/api/portal/auth/accept-invite.post.ts` refuses
    // before any call is possible, so there is no API wording to read. Everything else —
    // expired, already accepted — renders what the API said, in the caller's own language.
    error.value = signInRequired.value
      ? t('client.acceptInvite.signInRequired')
      : apiErrorMessage(err)
  } finally {
    loading.value = false
  }
}
</script>
