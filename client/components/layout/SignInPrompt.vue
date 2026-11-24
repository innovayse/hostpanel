<template>
  <!--
    A full-width band under the demo notice, its content in the header's UiContainer. Shown when the
    sign-in callback sent the visitor back with `?signin=required`: the identity provider found no
    session to sign them in with, and a page that just rendered as a guest left them wondering
    whether the portal had forgotten them.
  -->
  <UiStack
    v-if="visible"
    gap="none"
    role="status"
    class="w-full border-b py-2"
    :class="tone.wrap"
    data-testid="signin-prompt"
  >
    <UiContainer>
      <UiStack direction="row" align="center" justify="between" gap="none" wrap class="min-w-0 gap-x-3 gap-y-2">
        <UiText :as="UiTextTag.Span" size="sm" tone="inherit" class="min-w-0 break-words" :class="tone.body" data-testid="signin-prompt-text">
          {{ $t('signInPrompt.message') }}
        </UiText>
        <UiStack direction="row" align="center" gap="sm" class="shrink-0">
          <UiButton :href="signInHref" variant="primary" size="sm" data-testid="signin-prompt-signin">
            {{ $t('common.signIn') }}
          </UiButton>
          <UiButton variant="ghost" size="sm" data-testid="signin-prompt-dismiss" @click="dismiss">
            {{ $t('ui.close') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The sign-in prompt a page shows after a silent sign-in found no session.
 *
 * The button starts an interactive sign-in that returns to this page; an `authuser` address on the
 * page is passed on, so the identity provider offers that account. Dismissing removes the flag
 * from the address and leaves the visitor where they are, as a guest.
 */
import { LOGIN_HINT_QUERY, SIGN_IN_PROMPT_QUERY, SIGN_IN_PROMPT_VALUE } from '~/constants/clientRoutes'
import { SessionCookie } from '~/constants/sessionCookie'
import { UiAlertVariant, UiTextTag } from '~/utils/ui/enums'
import { ALERT_TONE_CLASSES } from '~/utils/ui/tones'
import { signInPath, withoutSignInPrompt } from '~/utils/returnPath'

/** The info palette the band shares with UiAlert. */
const tone = ALERT_TONE_CLASSES[UiAlertVariant.Info]

/** The current route, read for the flag and the account hint. */
const route = useRoute()

/** The router, used to drop the flag from the address on dismiss. */
const router = useRouter()

/** The readable signed-in hint; a signed-in visitor is never prompted. */
const authed = useCookie(SessionCookie.Authed)

/** Whether the prompt is shown: the flag is on the address and nobody is signed in. */
const visible = computed<boolean>(() => route.query[SIGN_IN_PROMPT_QUERY] === SIGN_IN_PROMPT_VALUE && !authed.value)

/** The interactive sign-in that returns to this page, with the account hint when there is one. */
const signInHref = computed<string>(() => {
  const hint = route.query[LOGIN_HINT_QUERY]
  return signInPath(route.fullPath, typeof hint === 'string' ? hint : '')
})

/**
 * Hides the prompt by removing the flag from the address.
 *
 * @returns Resolves once the navigation has been requested.
 */
const dismiss = async (): Promise<void> => {
  await router.replace(withoutSignInPrompt(route.fullPath))
}
</script>
