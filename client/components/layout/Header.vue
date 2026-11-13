<template>
  <header
    class="sticky top-0 z-40 border-b border-classic-border bg-classic-surface/95 backdrop-blur transition-shadow duration-200"
    :class="scrolled ? 'shadow-sm' : ''"
    data-testid="layout-header"
  >
    <UiContainer>
      <UiStack direction="row" align="center" gap="none" class="h-16 gap-3 xl:h-[72px] xl:gap-6">
        <!-- Brand: the operator's uploaded logo, or the mark beside the site name. -->
        <UiLink
          :to="localePath('/')"
          tone="inherit"
          :underline="false"
          class="flex min-w-0 shrink-0 items-center gap-2.5 rounded-lg text-classic-ink focus-visible:ring-classic-brand"
          testid="layout-logo"
        >
          <!-- Logo image cap: intrinsically small UI, not page layout. -->
          <LogoLockup
            img-class="h-9 w-auto max-w-[160px] object-contain"
            mark-class="h-9 w-9"
            name-class="truncate text-lg font-bold tracking-tight"
          />
        </UiLink>

        <!-- Desktop navigation (1280px and up). -->
        <UiStack
          as="nav"
          direction="row"
          align="center"
          justify="center"
          gap="none"
          class="hidden flex-auto gap-1 xl:flex"
          :aria-label="t('nav.primary')"
          data-testid="layout-desktop-nav"
        >
          <template v-for="item in navItems" :key="item.key">
            <LayoutHeaderDropdown v-if="item.children" :item="item" :path="route.path" :active="item.key === activeKey" />
            <UiLink
              v-else
              :to="item.to"
              tone="inherit"
              :underline="false"
              :class="[HEADER_NAV_ITEM, item.key === activeKey ? HEADER_NAV_ACTIVE : HEADER_NAV_IDLE]"
              :aria-current="item.key === activeKey ? 'page' : undefined"
              :testid="`header-nav-${item.key}`"
            >
              {{ item.label }}
            </UiLink>
          </template>
        </UiStack>

        <!-- Desktop controls: preferences, cart, then the account or the sign-in pair. -->
        <UiStack direction="row" align="center" gap="none" class="ml-auto hidden gap-2 xl:flex" data-testid="layout-desktop-actions">
          <UiLanguageSwitcher :trigger-class="PICKER_CLASS" />
          <UiCurrencySwitcher :trigger-class="PICKER_CLASS" />
          <!-- Light/dark switch: hidden by the component when the operator turns the toggle off. -->
          <UiThemeToggle variant="bare" size="lg" :class="HEADER_CONTROL" />
          <LayoutCartButton />

          <!-- The optional shell header widget fills its two mounts where the deployment serves it;
               everywhere else the local account menu or the sign-in pair is drawn. -->
          <template v-if="widgetEnabled">
            <UiStack id="inno-launcher-mount" gap="none" />
            <UiStack id="inno-account-mount" gap="none" />
          </template>
          <LayoutHeaderAccount v-else-if="isLoggedIn" />
          <template v-else>
            <!-- A plain anchor: under SSO the sign-in address is a server route the router cannot match. -->
            <UiButton
              variant="bare"
              :href="signInHref"
              :class="[HEADER_CONTROL, 'px-4 text-sm font-semibold']"
              data-testid="layout-sign-in"
            >
              {{ t('nav.signIn') }}
            </UiButton>
            <UiButton
              variant="bare"
              :to="localePath('/hosting')"
              :class="HEADER_PRIMARY"
              data-testid="layout-get-started"
            >
              {{ t('nav.getStarted') }}
            </UiButton>
          </template>
        </UiStack>

        <!-- Below 1280px: theme, cart and the menu button. -->
        <UiStack direction="row" align="center" gap="none" class="ml-auto gap-2 xl:hidden">
          <UiThemeToggle variant="bare" size="lg" :class="HEADER_CONTROL" />
          <LayoutCartButton />
          <UiIconButton
            :icon="Menu"
            :label="t('nav.openMenu')"
            variant="bare"
            size="lg"
            :class="HEADER_CONTROL"
            aria-haspopup="dialog"
            :aria-expanded="menuOpen"
            testid="layout-mobile-toggle"
            @click="menuOpen = true"
          />
        </UiStack>
      </UiStack>
    </UiContainer>

    <LayoutHeaderDrawer v-model="menuOpen" :items="navItems" :path="route.path" />
  </header>
</template>

<script setup lang="ts">
/**
 * LayoutHeader: the classic template's site header.
 *
 * A white bar with one brand colour: the shared logo lockup, the catalogue-driven navigation
 * (Hosting as a dropdown of the plan groups including VPS and dedicated servers, Domains,
 * Products, Support), then language and currency pickers, theme toggle, cart and either the
 * account menu (signed in) or Sign in plus Get started (signed out); where the optional shell
 * header widget is served, its two mount points replace that last part. Every control shares one
 * height, radius and border (`utils/headerControl.ts`). Below 1280px the navigation and the
 * account move into a side drawer.
 */
import { storeToRefs } from 'pinia'
import { Menu } from 'lucide-vue-next'
import LogoLockup from '~/components/LogoLockup.vue'
import LayoutCartButton from '~/components/layout/CartButton.vue'
import LayoutHeaderAccount from '~/components/layout/HeaderAccount.vue'
import LayoutHeaderDrawer from '~/components/layout/HeaderDrawer.vue'
import LayoutHeaderDropdown from '~/components/layout/HeaderDropdown.vue'
import { useAuthStore } from '~/stores/auth'
import { useHeaderWidget } from '~/composables/useHeaderWidget'
import { useCartStore } from '~/stores/cart'
import { useCatalogStore } from '~/stores/catalog'
import { HEADER_CONTROL, HEADER_NAV_ACTIVE, HEADER_NAV_IDLE, HEADER_NAV_ITEM, HEADER_PRIMARY } from '~/utils/headerControl'
import { activeHeaderKey, buildHeaderNav } from '~/utils/headerNav'

/** Scroll distance (px) after which the bar gets its shadow. */
const SCROLL_SHADOW_THRESHOLD = 8

/** Frame of the language and currency triggers: the shared control with room for text. */
const PICKER_CLASS = `items-center gap-2 px-3 ${HEADER_CONTROL}`

/** Translate function. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** Current route; its path marks the current entry and a change closes the drawer. */
const route = useRoute()

/** Where "Sign in" goes under the configured auth mode. */
const { signInHref } = useAuthMode()

/** Whether the optional shell header widget is served to this deployment (same rule as the client area top bar). */
const { enabled: widgetEnabled } = useHeaderWidget()

/** Whether a client is signed in. */
const { isLoggedIn } = storeToRefs(useAuthStore())

/** Cart store: its items are loaded from storage when the header mounts. */
const cartStore = useCartStore()

/** Catalogue store: the plan groups the navigation is built from. */
const catalogStore = useCatalogStore()

/** The catalogue's family groups and their products, which decide the navigation. */
const { familyGroups, familyProducts } = storeToRefs(catalogStore)

/** Whether the drawer is open. */
const menuOpen = ref(false)

/** Whether the page has scrolled far enough for the bar to carry a shadow. */
const scrolled = ref(false)

/** The navigation entries, rebuilt when the catalogue or the language changes. */
const navItems = computed(() => buildHeaderNav({
  t,
  localePath,
  groups: familyGroups.value,
  products: familyProducts.value,
}))

/** Key of the one entry that stands for the current page. */
const activeKey = computed(() => activeHeaderKey(route.path, navItems.value))

/** Records the scroll position for the bar's shadow. */
const handleScroll = (): void => {
  scrolled.value = window.scrollY > SCROLL_SHADOW_THRESHOLD
}

// Read during server rendering (the server awaits it and ships the state to the browser), so the
// Hosting dropdown entries are in the first paint. The store guard skips the read when a page has
// already loaded the families, so the catalogue is requested once.
useAsyncData('header:families', async () => {
  if (familyProducts.value.length === 0) {
    await catalogStore.loadFamilies()
  }
  return true
})

onMounted(() => {
  window.addEventListener('scroll', handleScroll, { passive: true })
  handleScroll()
  cartStore.init()
  // The optional shell header widget reads the accounts the sign-in callback saved.
  const widgetHost = window as unknown as { InnoWidget?: { reinit: () => void } }
  widgetHost.InnoWidget?.reinit?.()
})

onBeforeUnmount(() => {
  window.removeEventListener('scroll', handleScroll)
})

// A client-side navigation leaves the drawer open over the new page otherwise.
watch(() => route.path, () => {
  menuOpen.value = false
})
</script>
