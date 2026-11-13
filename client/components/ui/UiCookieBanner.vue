<template>
  <!--
    Clearance in the page flow, on a phone only: there the card spans the full width and sits over the last lines
    of the page, so the page ends above it. From `sm` the card is a small corner card that floats over the page,
    and a spacer would only show as an empty band under the footer.
  -->
  <div
    v-if="showBanner"
    aria-hidden="true"
    class="h-[var(--cookie-banner-height,0px)] shrink-0 sm:hidden"
    data-testid="cookie-banner-spacer"
  />

  <Transition
    enter-active-class="transition-all duration-[400ms] ease-out"
    leave-active-class="transition-all duration-[400ms] ease-in"
    enter-from-class="translate-y-full opacity-0"
    leave-to-class="translate-y-full opacity-0"
  >
    <div
      v-if="showBanner"
      ref="bannerRef"
      role="dialog"
      data-testid="cookie-banner"
      class="fixed bottom-3 left-3 right-3 z-50 p-4 sm:bottom-5 sm:left-auto sm:right-5 sm:w-[400px] rounded-2xl bg-white/95 dark:bg-ui-surface backdrop-blur-xl border border-gray-200 dark:border-white/10 shadow-xl"
    >
      <div>
        <div class="flex flex-col items-stretch gap-3">
          <!-- Content -->
          <div class="flex-1 flex items-start gap-4">
            <div class="hidden sm:block p-2 rounded-lg bg-primary-500/10">
              <Cookie :size="18" :stroke-width="2" class="text-primary-600 dark:text-primary-400" />
            </div>
            <div class="flex-1">
              <h3 class="hidden sm:block text-sm font-semibold text-gray-900 dark:text-white mb-1">
                {{ $t('cookieBanner.title') }}
              </h3>
              <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
                {{ $t('cookieBanner.descriptionShort') }}
                <UiLink :to="localePath('/cookie-policy')" testid="cookie-banner-learn-more">
                  {{ $t('cookieBanner.learnMore') }}
                </UiLink>
              </p>
            </div>
          </div>

          <!-- Buttons -->
          <div class="flex flex-row gap-2 w-full">
            <UiButton
              variant="subtle"
              size="sm"
              class="flex-1 whitespace-nowrap"
              data-testid="cookie-banner-essential"
              @click="acceptEssential"
            >
              {{ $t('cookieBanner.essentialOnly') }}
            </UiButton>
            <UiButton
              size="sm"
              class="flex-1 whitespace-nowrap"
              data-testid="cookie-banner-accept-all"
              @click="acceptAll"
            >
              {{ $t('cookieBanner.acceptAll') }}
            </UiButton>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
/**
 * CookieBanner (`UiCookieBanner`): the consent card floating in the bottom corner of the page
 * (full width less a gutter on a phone), small enough that it never sits over the page's content.
 *
 * Mounted by the `default` and `client` layouts only, so auth pages (no layout) never show it.
 * From `sm` it floats over the page like a toast and takes no space in the flow (an in-flow spacer used
 * to leave a blank band of its height under the footer on every page). On a phone, where the card spans
 * the width, a spacer (hidden from `sm`) keeps the last lines of the page above it. While visible it
 * publishes its height, so the floating contact button lifts clear of it.
 *
 * Shown only until a choice is made. The choice itself lives in `useCookieConsent`, where
 * `plugins/tracking.client.ts` reads it: this banner records the answer, the tag loader acts on it.
 */
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Cookie } from 'lucide-vue-next'
import { CookieConsent } from '~/types/cookieconsent'
import { COOKIE_BANNER_HEIGHT_VAR } from '~/utils/ui/cookieBanner'

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()

/** Whether the banner is showing. */
const showBanner = ref(false)

/** The banner's root element; measured so the phone spacer and the contact button match its real height. */
const bannerRef = ref<HTMLElement | null>(null)

/** Watches the banner's size; null while it is not shown. */
let resizeObserver: ResizeObserver | null = null

/**
 * Publishes the banner's current height on the root element, or clears it when there is none.
 *
 * @param height The height in pixels, or null to remove the variable.
 */
const publishHeight = (height: number | null): void => {
  if (height === null) {
    document.documentElement.style.removeProperty(COOKIE_BANNER_HEIGHT_VAR)
    return
  }
  document.documentElement.style.setProperty(COOKIE_BANNER_HEIGHT_VAR, `${height}px`)
}

// Follows the banner while it is mounted and stops, clearing the variable, once it is gone.
watch(bannerRef, (element) => {
  resizeObserver?.disconnect()
  resizeObserver = null
  if (!element) {
    publishHeight(null)
    return
  }
  publishHeight(element.offsetHeight)
  if (typeof ResizeObserver !== 'undefined') {
    resizeObserver = new ResizeObserver(() => publishHeight(element.offsetHeight))
    resizeObserver.observe(element)
  }
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  publishHeight(null)
})

/** The visitor's stored cookie choice and the action that records a new one. */
const { consent, accept } = useCookieConsent()

// Mounted rather than computed from `consent` on the server, so a page never renders the banner
// into the HTML for a visitor who already chose.
onMounted(() => {
  if (!consent.value) {
    showBanner.value = true
  }
})

/** Accepts essential cookies only: the tag manager is never loaded. */
const acceptEssential = (): void => {
  accept(CookieConsent.Essential)
  showBanner.value = false
}

/** Accepts all cookies: the tag manager loads if the operator configured one. */
const acceptAll = (): void => {
  accept(CookieConsent.All)
  showBanner.value = false
}
</script>
