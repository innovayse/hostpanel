<template>
  <header
    class="relative flex flex-wrap items-center justify-between gap-x-6 gap-y-4 border-b border-line px-[clamp(16px,4vw,48px)] py-[18px] font-aurora"
  >
    <NuxtLink :to="localePath('/')" class="flex items-center gap-3 text-tx">
      <span
        class="grid h-[34px] w-[34px] place-items-center rounded-[10px] bg-brand text-[17px] font-extrabold text-[#08090F]"
      >i</span>
      <span class="text-[19px] font-bold -tracking-[0.01em]">Innovayse</span>
    </NuxtLink>

    <nav class="hidden min-w-0 flex-auto flex-wrap items-center gap-x-[26px] gap-y-3 text-[15px] text-mut xl:flex">
      <NuxtLink
        v-for="link in navLinks"
        :key="link.key"
        :to="link.to"
        class="hover:text-ac1"
        :class="active === link.key ? 'font-semibold text-tx' : 'font-normal'"
      >{{ link.label }}</NuxtLink>
    </nav>

    <div class="flex flex-shrink-0 items-center gap-2.5">
      <AuroraAppLauncher />
      <UiLanguageSwitcher />
      <UiThemeToggle />

      <NuxtLink
        :to="localePath('/cart')"
        :title="t('aurora.nav.cart')"
        class="relative grid h-10 w-10 place-items-center rounded-[11px] border border-line2 text-tx2"
      >
        <Icon name="lucide:shopping-cart" class="h-[19px] w-[19px]" />
        <span
          v-if="cartCount > 0"
          class="absolute -right-1.5 -top-1.5 grid h-[18px] min-w-[18px] place-items-center rounded-full bg-brand px-1 text-[11px] font-extrabold text-[#08090F]"
        >{{ cartCount }}</span>
      </NuxtLink>

      <!--
        Signed in and signed out are two different headers, not one header with a
        different label. The session is known during server rendering (`authed`
        is a plain, readable cookie), so this needs no ClientOnly wrapper.
      -->
      <AuroraAccountMenu v-if="isLoggedIn" />

      <template v-else>
        <!--
          A plain anchor, not NuxtLink: under AUTH_MODE=sso this resolves to the
          Nitro route that starts the OIDC flow, and the router would try to
          match it against the client bundle instead of leaving the page.
        -->
        <a
          :href="signInHref"
          class="hidden px-1.5 text-[15px] text-mut hover:text-ac1 sm:inline-flex"
        >{{ t('aurora.nav.login') }}</a>

        <NuxtLink
          :to="localePath('/hosting')"
          class="hidden whitespace-nowrap rounded-[10px] bg-brand px-5 py-[11px] text-[15px] font-semibold text-[#08090F] hover:brightness-110 sm:inline-flex"
        >{{ t('aurora.nav.start') }}</NuxtLink>
      </template>

      <button
        type="button"
        :title="t('aurora.nav.menu')"
        class="grid h-10 w-10 place-items-center rounded-[11px] border border-line2 text-tx2 xl:hidden"
        @click="menuOpen = !menuOpen"
      >
        <Icon name="lucide:menu" class="h-[19px] w-[19px]" />
      </button>
    </div>
  </header>

  <div
    v-if="menuOpen"
    class="relative z-30 flex flex-col gap-0.5 border-b border-line bg-panel px-[clamp(16px,4vw,40px)] pb-5 pt-3 font-aurora xl:hidden"
  >
    <NuxtLink
      v-for="link in navLinks"
      :key="link.key"
      :to="link.to"
      class="rounded-[10px] px-2 py-3 text-base text-tx"
      @click="menuOpen = false"
    >{{ link.label }}</NuxtLink>
  </div>
</template>

<script setup lang="ts">
/**
 * aurora template site header.
 *
 * Presentation only. The locale menu, colour-mode toggle, apps launcher and
 * account menu are the portal's existing components; this restyles their
 * surroundings rather than reimplementing them.
 *
 * Two things it does read: whether there is a session, which decides between the
 * account menu and the sign-in pair, and the configured sign-in destination.
 */
import AuroraAppLauncher from '~/templates/aurora/layout/AppLauncher.vue'
import AuroraAccountMenu from '~/templates/aurora/layout/AccountMenu.vue'
import { useCartStore } from '~/stores/cart'

/** Which navigation entry to mark as current. */
const props = withDefaults(defineProps<{ active?: string }>(), { active: '' })

const { active } = toRefs(props)
const { t } = useI18n()
const localePath = useLocalePath()
const cart = useCartStore()

// Where "Sign in" goes is a deployment decision, not a constant: `sso` starts the
// OIDC flow against the platform identity provider, `local` opens this product's
// own form. This header used to hard-code the latter, so an SSO deployment sent
// every visitor to a login page its users have no credentials for.
const { signInHref } = useAuthMode()
const { isLoggedIn } = storeToRefs(useAuthStore())

const menuOpen = ref(false)

const cartCount = computed(() => cart.items.length)

const navLinks = computed(() => [
  { key: 'hosting', label: t('aurora.nav.hosting'), to: localePath('/hosting') },
  { key: 'domains', label: t('aurora.nav.domains'), to: localePath('/domains') },
  { key: 'services', label: t('aurora.nav.services'), to: localePath('/products') },
  { key: 'process', label: t('aurora.nav.process'), to: localePath('/contact') },
  { key: 'faq', label: t('aurora.nav.faq'), to: localePath('/faq') },
])
</script>
