<template>
  <!--
    z-[55] puts the bar, and the menu panel that hangs off it, above
    UiCookieBanner. The banner is fixed at z-50 and grows tall on a narrow
    screen, at 320px it covers the lower half of the viewport, so at z-40 the
    open menu was drawn beneath it and its lower entries could not be tapped.
    The floating contact button sits above both at z-[60]; it shares no space
    with the header, so the order between them never shows.
  -->
  <header class="tpl-nova sticky top-0 z-[55] border-b border-nova-border bg-nova-bg/95 font-nova backdrop-blur">
    <UiLink
      :href="`#${MAIN_CONTENT_ID}`"
      tone="inherit"
      :underline="false"
      testid="nova-skip-link"
      class="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-nova-accent focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-nova-on-accent"
    >
      {{ t('nova.nav.skipToContent') }}
    </UiLink>

    <UiContainer class="flex items-center gap-4 py-3">
      <UiLink
        :to="localePath(SITE_HOME_PATH)"
        tone="inherit"
        :underline="false"
        testid="nova-nav-logo"
        class="flex shrink-0 items-center gap-2.5 text-nova-ink"
      >
        <!-- The operator's upload when there is one, otherwise the cube mark and the site name. -->
        <!-- Logo image cap: intrinsically small UI, not page layout. -->
        <LogoLockup
          img-class="h-9 w-auto max-w-[120px] object-contain"
          mark-class="h-9 w-9"
          name-class="text-lg font-bold tracking-tight"
        />
      </UiLink>

      <!--
        The desktop layout starts at xl, not lg, and aurora's header switches at
        the same point for the same reason: at 1024 the six links, the logo and
        the full action row come to more than the container can hold, and the
        row has no shrink path: it would run past the edge and give the page a
        horizontal scrollbar. Below xl the links live in the menu panel.
      -->
      <nav :aria-label="t('nova.nav.menu')" class="hidden flex-auto items-center justify-center gap-1 xl:flex">
        <UiLink
          v-for="item in NAV_ITEMS"
          :key="item.key"
          :to="localePath(item.to)"
          tone="inherit"
          :underline="false"
          :testid="`nova-nav-${item.key}`"
          class="rounded-lg px-3 py-2 text-[15px] transition-colors hover:bg-nova-surface-2 hover:text-nova-ink focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-nova-brand"
          :class="active === item.key ? 'font-semibold text-nova-ink' : 'text-nova-muted'"
          :aria-current="active === item.key ? 'page' : undefined"
        >
          {{ t(item.labelKey) }}
        </UiLink>
      </nav>

      <UiStack direction="row" align="center" gap="none" class="ml-auto gap-1.5 xl:ml-0">
        <!--
          Locale and theme are bar controls only where the bar has room for them.
          Below xl they move into the menu panel: together they are about 130px
          of fixed-width chrome, and with the logo, the cart and the menu button
          the row could not fit a 390px screen: it did not wrap or shrink, it
          simply ran past the edge and gave the whole page a horizontal scroll.
          Neither control is lost; both are the first thing in the panel.
        -->
        <UiStack direction="row" align="center" gap="none" class="hidden gap-1.5 xl:flex">
          <UiLanguageSwitcher />
          <UiCurrencySwitcher />
          <UiThemeToggle />
        </UiStack>

        <UiLink
          :to="localePath(SITE_CART_PATH)"
          tone="inherit"
          :underline="false"
          testid="nova-nav-cart"
          :aria-label="t('nova.nav.cart')"
          class="relative grid h-11 w-11 place-items-center rounded-xl border border-nova-border text-nova-ink hover:border-nova-brand"
        >
          <Icon name="lucide:shopping-cart" class="h-5 w-5" aria-hidden="true" />
          <span
            v-if="cartCount > 0"
            class="absolute -right-1 -top-1 grid h-[18px] min-w-[18px] place-items-center rounded-full bg-nova-accent px-1 text-[11px] font-extrabold text-nova-on-accent"
            data-testid="nova-nav-cart-count"
          >{{ cartCount }}</span>
        </UiLink>

        <!--
          A plain anchor, not a route link: under AUTH_MODE=sso `signInHref` is a
          Nitro route that starts the OIDC flow, and the router would try to match
          it against the client bundle instead of leaving the page.
        -->
        <NovaCta
          :href="signInHref"
          tone="quiet"
          size="bar"
          class="hidden px-3 sm:inline-flex"
          data-testid="nova-nav-login"
        >
          {{ t('nova.nav.login') }}
        </NovaCta>

        <NovaCta
          :to="localePath(SITE_HOSTING_PATH)"
          size="bar"
          class="hidden whitespace-nowrap px-5 sm:inline-flex"
          data-testid="nova-nav-get-started"
        >
          {{ t('nova.nav.getStarted') }}
        </NovaCta>

        <UiButton
          ref="toggleRef"
          variant="bare"
          class="h-11 w-11 rounded-xl border border-nova-border text-nova-ink xl:hidden"
          :aria-expanded="menuOpen"
          :aria-controls="MENU_ID"
          :aria-label="menuOpen ? t('nova.nav.close') : t('nova.nav.menu')"
          data-testid="nova-nav-menu-toggle"
          @click="toggleMenu"
        >
          <Icon :name="menuOpen ? 'lucide:x' : 'lucide:menu'" class="h-5 w-5" aria-hidden="true" />
        </UiButton>
      </UiStack>
    </UiContainer>

    <div
      v-if="menuOpen"
      :id="MENU_ID"
      ref="menuRef"
      class="border-t border-nova-border bg-nova-surface px-4 pb-5 pt-2 sm:px-6 xl:hidden"
      @keydown="onMenuKeydown"
    >
      <nav :aria-label="t('nova.nav.menu')" class="flex flex-col">
        <UiLink
          v-for="item in NAV_ITEMS"
          :key="item.key"
          :to="localePath(item.to)"
          tone="inherit"
          :underline="false"
          :testid="`nova-mobile-${item.key}`"
          class="flex min-h-[44px] items-center rounded-lg px-2 text-base text-nova-ink"
          :aria-current="active === item.key ? 'page' : undefined"
          @click="closeMenu"
        >
          {{ t(item.labelKey) }}
        </UiLink>
      </nav>

      <UiStack
        direction="row"
        align="center"
        justify="between"
        gap="sm"
        class="mt-3 border-t border-nova-border pt-3"
      >
        <UiText as="span" size="inherit" tone="inherit" class="text-sm text-nova-muted">
          {{ t('nova.nav.preferences') }}
        </UiText>
        <!--
          The locale and theme controls are shared components sized for a
          pointer: 36px tall, below the 44px a finger needs. The arbitrary
          variants raise their hit area here rather than in the components
          themselves, which aurora and classic also render and which this
          change must not reach. The nested selector deliberately matches only
          each control's own trigger, not the locale dropdown's list items.
        -->
        <UiStack
          direction="row"
          align="center"
          gap="xs"
          class="[&>div>button]:min-h-[44px] [&>div>button]:min-w-[44px] [&>button]:min-h-[44px] [&>button]:min-w-[44px]"
        >
          <UiLanguageSwitcher />
          <UiCurrencySwitcher />
          <UiThemeToggle />
        </UiStack>
      </UiStack>

      <UiStack gap="xs" class="mt-3 border-t border-nova-border pt-3">
        <NovaCta
          :href="signInHref"
          tone="outline"
          size="bar"
          data-testid="nova-mobile-login"
          @click="closeMenu"
        >
          {{ t('nova.nav.login') }}
        </NovaCta>
        <NovaCta
          :to="localePath(SITE_HOSTING_PATH)"
          size="bar"
          data-testid="nova-mobile-get-started"
          @click="closeMenu"
        >
          {{ t('nova.nav.getStarted') }}
        </NovaCta>
      </UiStack>
    </div>
  </header>
</template>

<script setup lang="ts">
/**
 * nova template site header.
 *
 * Presentation only: the locale menu, colour-mode toggle and cart count are the
 * portal's existing pieces, restyled rather than reimplemented. The navigation
 * itself comes from `content.ts`, so an entry is added there and nowhere else.
 *
 * The mobile panel is a disclosure, not a dialog, it sits in the page flow
 * under the bar rather than covering it, so it carries `aria-expanded` and
 * `aria-controls` and keeps focus inside itself while open, but does not claim
 * `role="dialog"` or hide the rest of the page from assistive technology.
 */
import type { ComponentPublicInstance } from 'vue'
import LogoLockup from '~/components/LogoLockup.vue'
import NovaCta from '~/templates/nova/sections/NovaCta.vue'
import { NAV_ITEMS } from '~/templates/nova/content'
import { useCartStore } from '~/stores/cart'
import { SITE_CART_PATH, SITE_HOME_PATH, SITE_HOSTING_PATH } from '~/constants/siteRoutes'

/** Props of NovaHeader. */
const props = withDefaults(defineProps<{
  /** Which navigation entry to mark as current, by its key in `NAV_ITEMS`. */
  active?: string
}>(), { active: '' })

/** Id of the page's main region, so the skip link has something to point at. */
const MAIN_CONTENT_ID = 'nova-main'
/** Id of the mobile panel, referenced by the toggle's `aria-controls`. */
const MENU_ID = 'nova-mobile-menu'

/** The current-entry key as a ref, so the template reads it reactively. */
const { active } = toRefs(props)
/** Translator for the copy. */
const { t } = useI18n()
/** Prefixes a route with the active locale. */
const localePath = useLocalePath()
/** The cart, for the item count. */
const cart = useCartStore()
/** The current route, watched to close the panel on navigation. */
const route = useRoute()

/**
 * Where "Sign in" goes is a deployment decision, not a constant: see
 * `useAuthMode`. Hard-coding /client/login sent an SSO deployment's visitors to a
 * form their accounts have no credentials for.
 */
const { signInHref } = useAuthMode()

/** Whether the mobile panel is open. */
const menuOpen = ref(false)

/** The open panel, searched for focusable entries. */
const menuRef = ref<HTMLElement | null>(null)
/** The button that opens the panel; focus returns to it on close. */
const toggleRef = ref<ComponentPublicInstance | null>(null)

/** Number of lines in the cart. */
const cartCount = computed(() => cart.items.length)

/**
 * Everything inside the panel a keyboard can land on, in document order.
 *
 * @returns The focusable links and enabled buttons; empty while the panel is not mounted.
 */
const focusables = (): HTMLElement[] =>
  Array.from(menuRef.value?.querySelectorAll<HTMLElement>('a[href], button:not([disabled])') ?? [])

/**
 * Puts keyboard focus back on the button that opens the panel.
 */
const focusToggle = (): void => {
  (toggleRef.value?.$el as HTMLElement | undefined)?.focus()
}

/**
 * Opens or closes the panel; on open, moves focus to its first entry.
 */
const toggleMenu = async (): Promise<void> => {
  menuOpen.value = !menuOpen.value
  if (!menuOpen.value) {
    return
  }

  // The panel is rendered by v-if, so it does not exist until the next tick.
  await nextTick()
  focusables()[0]?.focus()
}

/**
 * Closes the panel and puts focus back on the button that opened it, so a
 * keyboard user is returned to where they were rather than to the top of the
 * document.
 */
const closeMenu = (): void => {
  if (!menuOpen.value) {
    return
  }
  menuOpen.value = false
  focusToggle()
}

/**
 * Keeps Tab inside the open panel and closes it on Escape.
 *
 * Without the wrap, tabbing past the last link walks into the page behind the
 * panel while the panel is still covering it on a small screen.
 *
 * @param event The key press inside the panel.
 */
const onMenuKeydown = (event: KeyboardEvent): void => {
  if (event.key === 'Escape') {
    closeMenu()
    return
  }

  if (event.key !== 'Tab') {
    return
  }

  const items = focusables()
  const first = items[0]
  const last = items.at(-1)
  if (!first || !last) {
    return
  }
  const current = document.activeElement

  if (event.shiftKey && current === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && current === last) {
    event.preventDefault()
    first.focus()
  }
}

/**
 * Closes the panel on Escape from anywhere on the page.
 *
 * Escape must work even before focus has entered the panel: the button that
 * opened it still holds focus at that point, and its keydown does not bubble
 * through the panel's handler.
 *
 * @param event The key press on the document.
 */
const onDocumentKeydown = (event: KeyboardEvent): void => {
  if (event.key === 'Escape') {
    closeMenu()
  }
}

onMounted(() => document.addEventListener('keydown', onDocumentKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', onDocumentKeydown))

// A route change leaves the panel open over the new page otherwise, because the
// links navigate client-side and nothing else unmounts the header.
watch(() => route.fullPath, () => { menuOpen.value = false })
</script>
