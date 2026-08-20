<template>
  <!--
    z-[55] puts the bar, and the menu panel that hangs off it, above
    UiCookieBanner. The banner is fixed at z-50 and grows tall on a narrow
    screen — at 320px it covers the lower half of the viewport — so at z-40 the
    open menu was drawn beneath it and its lower entries could not be tapped.
    The floating contact button sits above both at z-[60]; it shares no space
    with the header, so the order between them never shows.
  -->
  <header class="tpl-nova sticky top-0 z-[55] border-b border-nova-border bg-nova-bg/95 font-nova backdrop-blur">
    <a
      :href="`#${MAIN_CONTENT_ID}`"
      class="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-nova-accent focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-[#12212a]"
    >{{ t('nova.nav.skipToContent') }}</a>

    <div class="mx-auto flex max-w-[1240px] items-center gap-4 px-4 py-3 sm:px-6 lg:px-8">
      <NuxtLink :to="localePath('/')" class="flex shrink-0 items-center gap-2.5 text-nova-ink">
        <span
          class="grid h-9 w-9 place-items-center rounded-xl bg-[linear-gradient(135deg,var(--n-brand),var(--n-accent))] text-[17px] font-extrabold text-[#08191f]"
          aria-hidden="true"
        >i</span>
        <span class="text-lg font-bold tracking-tight">Innovayse</span>
      </NuxtLink>

      <!--
        The desktop layout starts at xl, not lg, and aurora's header switches at
        the same point for the same reason: at 1024 the six links, the logo and
        the full action row come to more than the container can hold, and the
        row has no shrink path — it ran 33px past the edge and took the page's
        horizontal scrollbar with it. Below xl the links live in the menu panel.
      -->
      <nav :aria-label="t('nova.nav.menu')" class="hidden flex-auto items-center justify-center gap-1 xl:flex">
        <NuxtLink
          v-for="item in NAV_ITEMS"
          :key="item.key"
          :to="localePath(item.to)"
          class="rounded-lg px-3 py-2 text-[15px] text-nova-muted transition-colors hover:bg-nova-surface-2 hover:text-nova-ink focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-nova-brand"
          :class="active === item.key ? 'font-semibold text-nova-ink' : ''"
          :aria-current="active === item.key ? 'page' : undefined"
        >{{ t(item.labelKey) }}</NuxtLink>
      </nav>

      <div class="ml-auto flex items-center gap-1.5 xl:ml-0">
        <!--
          Locale and theme are bar controls only where the bar has room for them.
          Below xl they move into the menu panel: together they are about 130px
          of fixed-width chrome, and with the logo, the cart and the menu button
          the row could not fit a 390px screen — it did not wrap or shrink, it
          simply ran past the edge and gave the whole page a horizontal scroll.
          Neither control is lost; both are the first thing in the panel.
        -->
        <div class="hidden items-center gap-1.5 xl:flex">
          <UiLanguageSwitcher />
          <UiThemeToggle />
        </div>

        <NuxtLink
          :to="localePath('/cart')"
          :aria-label="t('nova.nav.cart')"
          class="relative grid h-11 w-11 place-items-center rounded-xl border border-nova-border text-nova-ink transition-colors hover:border-nova-brand"
        >
          <Icon name="lucide:shopping-cart" class="h-5 w-5" aria-hidden="true" />
          <span
            v-if="cartCount > 0"
            class="absolute -right-1 -top-1 grid h-[18px] min-w-[18px] place-items-center rounded-full bg-nova-accent px-1 text-[11px] font-extrabold text-[#12212a]"
          >{{ cartCount }}</span>
        </NuxtLink>

        <NuxtLink
          :to="localePath('/client/login')"
          class="hidden min-h-[44px] items-center rounded-xl px-3 text-[15px] font-medium text-nova-muted transition-colors hover:text-nova-ink sm:inline-flex"
        >{{ t('nova.nav.login') }}</NuxtLink>

        <NuxtLink
          :to="localePath('/hosting')"
          class="hidden min-h-[44px] items-center whitespace-nowrap rounded-xl bg-nova-accent px-5 text-[15px] font-bold text-[#12212a] transition-[filter] hover:brightness-95 sm:inline-flex"
        >{{ t('nova.nav.getStarted') }}</NuxtLink>

        <button
          ref="toggleRef"
          type="button"
          class="grid h-11 w-11 place-items-center rounded-xl border border-nova-border text-nova-ink xl:hidden"
          :aria-expanded="menuOpen"
          :aria-controls="MENU_ID"
          :aria-label="menuOpen ? t('nova.nav.close') : t('nova.nav.menu')"
          @click="toggleMenu"
        >
          <Icon :name="menuOpen ? 'lucide:x' : 'lucide:menu'" class="h-5 w-5" aria-hidden="true" />
        </button>
      </div>
    </div>

    <div
      v-if="menuOpen"
      :id="MENU_ID"
      ref="menuRef"
      class="border-t border-nova-border bg-nova-surface px-4 pb-5 pt-2 sm:px-6 xl:hidden"
      @keydown="onMenuKeydown"
    >
      <nav :aria-label="t('nova.nav.menu')" class="flex flex-col">
        <NuxtLink
          v-for="item in NAV_ITEMS"
          :key="item.key"
          :to="localePath(item.to)"
          class="flex min-h-[44px] items-center rounded-lg px-2 text-base text-nova-ink"
          :aria-current="active === item.key ? 'page' : undefined"
          @click="closeMenu"
        >{{ t(item.labelKey) }}</NuxtLink>
      </nav>

      <div class="mt-3 flex items-center justify-between gap-3 border-t border-nova-border pt-3">
        <span class="text-sm text-nova-muted">{{ t('nova.nav.preferences') }}</span>
        <!--
          The locale and theme controls are shared components sized for a
          pointer — 36px tall, below the 44px a finger needs. The arbitrary
          variants raise their hit area here rather than in the components
          themselves, which aurora and classic also render and which this
          change must not reach. The nested selector deliberately matches only
          each control's own trigger, not the locale dropdown's list items.
        -->
        <div class="flex items-center gap-2 [&>div>button]:min-h-[44px] [&>div>button]:min-w-[44px] [&>button]:min-h-[44px] [&>button]:min-w-[44px]">
          <UiLanguageSwitcher />
          <UiThemeToggle />
        </div>
      </div>

      <div class="mt-3 flex flex-col gap-2 border-t border-nova-border pt-3">
        <NuxtLink
          :to="localePath('/client/login')"
          class="flex min-h-[44px] items-center justify-center rounded-xl border border-nova-border text-[15px] font-semibold text-nova-ink"
          @click="closeMenu"
        >{{ t('nova.nav.login') }}</NuxtLink>
        <NuxtLink
          :to="localePath('/hosting')"
          class="flex min-h-[44px] items-center justify-center rounded-xl bg-nova-accent text-[15px] font-bold text-[#12212a]"
          @click="closeMenu"
        >{{ t('nova.nav.getStarted') }}</NuxtLink>
      </div>
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
 * The mobile panel is a disclosure, not a dialog — it sits in the page flow
 * under the bar rather than covering it — so it carries `aria-expanded` and
 * `aria-controls` and keeps focus inside itself while open, but does not claim
 * `role="dialog"` or hide the rest of the page from assistive technology.
 */
import { NAV_ITEMS } from '~/templates/nova/content'
import { useCartStore } from '~/stores/cart'

/** Which navigation entry to mark as current, by its key in `NAV_ITEMS`. */
const props = withDefaults(defineProps<{ active?: string }>(), { active: '' })

/** Id of the page's main region, so the skip link has something to point at. */
const MAIN_CONTENT_ID = 'nova-main'
const MENU_ID = 'nova-mobile-menu'

const { active } = toRefs(props)
const { t } = useI18n()
const localePath = useLocalePath()
const cart = useCartStore()
const route = useRoute()

const menuOpen = ref(false)
const menuRef = ref<HTMLElement | null>(null)
const toggleRef = ref<HTMLButtonElement | null>(null)

const cartCount = computed(() => cart.items.length)

/** Everything inside the panel a keyboard can land on, in document order. */
const focusables = (): HTMLElement[] =>
  Array.from(menuRef.value?.querySelectorAll<HTMLElement>('a[href], button:not([disabled])') ?? [])

const toggleMenu = async () => {
  menuOpen.value = !menuOpen.value
  if (!menuOpen.value) return

  // The panel is rendered by v-if, so it does not exist until the next tick.
  await nextTick()
  focusables()[0]?.focus()
}

/**
 * Closes the panel and puts focus back on the button that opened it, so a
 * keyboard user is returned to where they were rather than to the top of the
 * document.
 */
const closeMenu = () => {
  if (!menuOpen.value) return
  menuOpen.value = false
  toggleRef.value?.focus()
}

/**
 * Keeps Tab inside the open panel and closes it on Escape.
 *
 * Without the wrap, tabbing past the last link walks into the page behind the
 * panel while the panel is still covering it on a small screen.
 */
const onMenuKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape') {
    closeMenu()
    return
  }

  if (event.key !== 'Tab') return

  const items = focusables()
  if (items.length === 0) return

  const first = items[0]!
  const last = items[items.length - 1]!
  const current = document.activeElement

  if (event.shiftKey && current === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && current === last) {
    event.preventDefault()
    first.focus()
  }
}

// Escape must work even before focus has entered the panel — the button that
// opened it still holds focus at that point, and its keydown does not bubble
// through the panel's handler.
const onDocumentKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape') closeMenu()
}

onMounted(() => document.addEventListener('keydown', onDocumentKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', onDocumentKeydown))

// A route change leaves the panel open over the new page otherwise, because the
// links navigate client-side and nothing else unmounts the header.
watch(() => route.fullPath, () => { menuOpen.value = false })
</script>
