<template>
  <UiBlock :as="UiBlockTag.Header" class="relative border-b border-line font-aurora">
    <UiContainer class="flex flex-wrap items-center justify-between gap-x-6 gap-y-4 py-[18px]">
      <UiLink
        :to="localePath(SITE_HOME_PATH)"
        tone="inherit"
        :underline="false"
        testid="site-home"
        class="flex items-center gap-3 text-tx"
      >
        <!-- The operator's upload when there is one, otherwise the cube mark and the site name. -->
        <!-- Logo image cap: intrinsically small UI, not page layout. -->
        <LogoLockup
          img-class="h-[34px] w-auto max-w-[140px]"
          mark-class="h-9 w-9"
          name-class="text-lg font-bold"
        />
      </UiLink>

      <UiStack
        :as="UiStackTag.Nav"
        direction="row"
        align="center"
        wrap
        class="hidden min-w-0 flex-auto gap-x-[26px] gap-y-3 text-[15px] xl:flex"
      >
        <UiLink
          v-for="link in navLinks"
          :key="link.key"
          :to="link.to"
          tone="inherit"
          :underline="false"
          :testid="`nav-${link.key}`"
          :aria-current="activeKey === link.key ? 'page' : undefined"
          :class="[NAV_LINK, activeKey === link.key ? NAV_LINK_ACTIVE : 'font-normal']"
        >
          {{ link.label }}
        </UiLink>
      </UiStack>

      <UiStack direction="row" align="center" gap="xs" class="shrink-0">
        <AuroraAppLauncher />
        <UiLanguageSwitcher :trigger-class="PICKER_CONTROL" />
        <UiCurrencySwitcher :trigger-class="PICKER_CONTROL" />
        <UiThemeToggle :variant="UiIconButtonVariant.Bare" :size="UiSize.Lg" :class="ICON_CONTROL" />

        <UiButton
          variant="bare"
          :to="localePath(SITE_CART_PATH)"
          :title="t('aurora.nav.cart')"
          :aria-label="t('aurora.nav.cart')"
          data-testid="nav-cart"
          :class="[ICON_CONTROL, 'relative']"
        >
          <Icon name="lucide:shopping-cart" class="h-[19px] w-[19px]" />
          <UiTile
            v-if="cartCount > 0"
            size="sm"
            rounded
            class="absolute -right-2 -top-2 z-10 bg-brand font-extrabold text-on-brand ring-2 ring-page"
            data-testid="nav-cart-count"
          >
            {{ cartCount }}
          </UiTile>
        </UiButton>

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
          <UiLink
            :href="signInHref"
            tone="inherit"
            :underline="false"
            testid="nav-sign-in"
            :class="[NAV_LINK, 'hidden text-[15px] sm:inline-flex']"
          >
            {{ t('common.signIn') }}
          </UiLink>

          <UiButton
            variant="bare"
            :to="localePath(SITE_HOSTING_PATH)"
            data-testid="nav-start"
            class="hidden whitespace-nowrap rounded-xl bg-brand px-5 py-[11px] text-[15px] font-semibold text-on-brand hover:brightness-110 sm:inline-flex"
          >
            {{ t('aurora.actions.getStarted') }}
          </UiButton>
        </template>

        <UiButton
          variant="bare"
          :title="t('aurora.nav.menu')"
          :aria-label="t('aurora.nav.menu')"
          :aria-expanded="menuOpen"
          data-testid="nav-menu-toggle"
          :class="[ICON_CONTROL, 'xl:hidden']"
          @click="menuOpen = !menuOpen"
        >
          <Icon name="lucide:menu" class="h-[19px] w-[19px]" />
        </UiButton>
      </UiStack>
    </UiContainer>
  </UiBlock>

  <UiBlock
    v-if="menuOpen"
    data-testid="nav-mobile-menu"
    class="relative z-30 border-b border-line bg-panel font-aurora xl:hidden"
  >
    <UiContainer class="pb-5 pt-3">
      <UiStack :as="UiStackTag.Nav" gap="none">
        <UiLink
          v-for="link in navLinks"
          :key="link.key"
          :to="link.to"
          tone="inherit"
          :underline="false"
          :testid="`nav-mobile-${link.key}`"
          :aria-current="activeKey === link.key ? 'page' : undefined"
          :class="['rounded-xl px-2 py-3 text-base transition-colors hover:text-ac1', activeKey === link.key ? 'font-semibold text-ac1' : 'text-tx']"
          @click="menuOpen = false"
        >
          {{ link.label }}
        </UiLink>

        <!--
          The bar hides the sign-in pair below the sm breakpoint, so the panel carries it
          there; signed in, the avatar stays in the bar and the panel adds the way to the
          client area.
        -->
        <UiLink
          v-if="isLoggedIn"
          :to="localePath(CLIENT_DASHBOARD_PATH)"
          tone="inherit"
          :underline="false"
          testid="nav-mobile-account"
          class="rounded-xl px-2 py-3 text-base font-semibold text-tx"
          @click="menuOpen = false"
        >
          {{ t('nav.clientArea') }}
        </UiLink>

        <UiStack v-else gap="sm" class="mt-3 border-t border-line pt-4 sm:hidden">
          <UiLink
            :href="signInHref"
            tone="inherit"
            :underline="false"
            testid="nav-mobile-sign-in"
            class="rounded-xl border border-line2 px-5 py-3 text-center text-base font-semibold text-tx"
            @click="menuOpen = false"
          >
            {{ t('common.signIn') }}
          </UiLink>

          <UiButton
            variant="bare"
            :to="localePath(SITE_HOSTING_PATH)"
            data-testid="nav-mobile-start"
            class="w-full rounded-xl bg-brand px-5 py-3 text-base font-semibold text-on-brand hover:brightness-110"
            @click="menuOpen = false"
          >
            {{ t('aurora.actions.getStarted') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiContainer>
  </UiBlock>
</template>

<script setup lang="ts">
/**
 * aurora template site header.
 *
 * Presentation only. The locale menu, colour-mode toggle, apps launcher and
 * account menu are the portal's existing components; this restyles their
 * surroundings rather than reimplementing them. The narrow-screen panel repeats the
 * sign-in pair (signed out) or links the client area (signed in), because the bar itself
 * drops them below the sm breakpoint.
 *
 * Two things it does read: whether there is a session, which decides between the
 * account menu and the sign-in pair, and the configured sign-in destination.
 */
import LogoLockup from '~/components/LogoLockup.vue'
import AuroraAppLauncher from '~/templates/aurora/layout/AppLauncher.vue'
import AuroraAccountMenu from '~/templates/aurora/layout/AccountMenu.vue'
import { CLIENT_DASHBOARD_PATH } from '~/constants/clientRoutes'
import { homeSectionFromHash, homeSectionPath } from '~/constants/homeSections'
import { HomeSectionId } from '~/types/homesectionid'
import { HEADER_CONTROL, NAV_LINK, NAV_LINK_ACTIVE } from '~/templates/aurora/buttons'
import { UiBlockTag, UiIconButtonVariant, UiSize, UiStackTag } from '~/utils/ui/enums'
import { useCartStore } from '~/stores/cart'
import {
  SITE_CART_PATH,
  SITE_DOMAINS_PATH,
  SITE_FAQ_PATH,
  SITE_HOME_PATH,
  SITE_HOSTING_PATH,
} from '~/constants/siteRoutes'

/** Props of the header. */
const props = withDefaults(defineProps<{
  /** Which navigation entry to mark as current. */
  active?: string
}>(), { active: '' })

/** The current navigation key as a ref. */
const { active } = toRefs(props)
/** Translator for the active locale. */
const { t } = useI18n()
/** Prefixes a path with the active locale. */
const localePath = useLocalePath()
/** The cart store. */
const cart = useCartStore()
/** The current route; its hash marks the home-page section the visitor jumped to. */
const route = useRoute()

/** Frame of the icon-only header controls (cart, theme toggle, menu toggle). */
const ICON_CONTROL = `${HEADER_CONTROL} w-10`
/** Frame of the header pickers (language, currency): the same box with room for text. */
const PICKER_CONTROL = `${HEADER_CONTROL} gap-2 px-3`

// Where "Sign in" goes is a deployment decision, not a constant: `sso` starts the
// OIDC flow against the platform identity provider, `local` opens this product's
// own form. Hard-coding the form would send every visitor of an SSO deployment to a
// login page its users have no credentials for.
/** Where the sign-in link goes under the configured auth mode. */
const { signInHref } = useAuthMode()
/** Whether a session exists. */
const { isLoggedIn } = storeToRefs(useAuthStore())

/** Whether the narrow-screen navigation panel is open. */
const menuOpen = ref(false)

/** Number of lines in the cart, shown on the cart button. */
const cartCount = computed(() => cart.items.length)

/**
 * The entry to mark as current: the section named by the route hash (Services, Process) when
 * there is one, otherwise the key the page passed in.
 */
const activeKey = computed(() => homeSectionFromHash(route.hash ?? '') ?? active.value)

/** Navigation entries, in display order. */
const navLinks = computed(() => [
  { key: 'hosting', label: t('aurora.nav.hosting'), to: localePath(SITE_HOSTING_PATH) },
  { key: 'domains', label: t('aurora.nav.domains'), to: localePath(SITE_DOMAINS_PATH) },
  { key: HomeSectionId.Services, label: t('aurora.nav.services'), to: homeSectionPath(localePath, HomeSectionId.Services) },
  { key: HomeSectionId.Process, label: t('aurora.nav.process'), to: homeSectionPath(localePath, HomeSectionId.Process) },
  { key: 'faq', label: t('aurora.nav.faq'), to: localePath(SITE_FAQ_PATH) },
])
</script>
