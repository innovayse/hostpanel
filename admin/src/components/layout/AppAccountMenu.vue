<script setup lang="ts">
/**
 * Account menu of the top bar: a round avatar button with the operator's initial that opens a
 * small panel with the full address (it wraps, so a long address never pushes the bar), a link to
 * the settings and the sign-out action.
 *
 * Keyboard behaviour follows the WAI-ARIA APG "Menu Button" pattern: opening moves focus to the
 * first item, ArrowDown / ArrowUp cycle the items, Escape closes the menu and returns focus to
 * the button. A click outside or choosing an item closes it.
 */
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiPanelTransition from '@/components/ui/UiPanelTransition.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'

/** Props for AppAccountMenu. */
const props = defineProps<{
  /** The signed-in operator's address, or a neutral label before the profile has loaded. */
  email: string
}>()

const emit = defineEmits<{
  /** Emitted when the operator chooses "Sign out". */
  'sign-out': []
}>()

const { t } = useI18n()

/** Whether the panel is open. */
const open = ref(false)

/** Root element, for outside-click detection. */
const root = ref<HTMLElement | null>(null)

/** The avatar button; focus returns here when the menu closes by keyboard. */
const triggerRef = ref<InstanceType<typeof UiButton> | null>(null)

/** The panel, for focus management. */
const menuRef = ref<HTMLElement | null>(null)

/** Upper-case first character of the address, shown in the avatar. */
const initial = computed<string>(() => props.email.trim().charAt(0).toUpperCase())

/** Closes the menu and returns focus to the button. */
const closeToTrigger = (): void => {
  open.value = false
  const trigger = triggerRef.value?.$el as HTMLElement | undefined
  trigger?.focus()
}

/** Chooses "Sign out": closes the menu and tells the bar. */
const signOut = (): void => {
  open.value = false
  emit('sign-out')
}

/**
 * Closes the menu when the pointer goes down outside it.
 *
 * @param e - The mouse event.
 */
const onOutsideClick = (e: MouseEvent): void => {
  if (root.value && !root.value.contains(e.target as Node)) {
    open.value = false
  }
}

/**
 * Handles Escape and the arrow keys while the menu is open.
 *
 * @param e - The keyboard event.
 */
const onKeydown = (e: KeyboardEvent): void => {
  if (!open.value) { return }
  if (e.key === 'Escape') {
    closeToTrigger()
    return
  }
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    e.preventDefault()
    const items = Array.from(menuRef.value?.querySelectorAll<HTMLElement>('a, button') ?? [])
    if (items.length === 0) { return }
    const index = items.indexOf(document.activeElement as HTMLElement)
    const step = e.key === 'ArrowDown' ? 1 : -1
    items[(index + step + items.length) % items.length]?.focus()
  }
}

// Focus the first item as soon as the panel is in the DOM.
watch(open, async (isOpen) => {
  if (!isOpen) { return }
  await nextTick()
  menuRef.value?.querySelector<HTMLElement>('a, button')?.focus()
})

onMounted(() => {
  document.addEventListener('mousedown', onOutsideClick)
  document.addEventListener('keydown', onKeydown)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', onOutsideClick)
  document.removeEventListener('keydown', onKeydown)
})

/** Classes shared by the two menu items. */
const itemClasses = 'flex w-full items-center rounded-lg px-3 py-2 text-left text-[0.82rem] transition-colors outline-none hover:bg-surface-hover focus-visible:bg-surface-hover'
</script>

<template>
  <div ref="root" class="relative">
    <UiButton
      ref="triggerRef"
      type="button"
      :aria-label="t('layout.account.menu')"
      :aria-expanded="open"
      aria-haspopup="menu"
      class="flex h-8 w-8 items-center justify-center rounded-full gradient-brand text-[0.82rem] font-semibold text-white outline-none transition-opacity hover:opacity-90 focus-visible:ring-2 focus-visible:ring-primary-500/60"
      data-testid="topbar-account"
      @click="open = !open"
    >
      {{ initial }}
    </UiButton>

    <UiPanelTransition>
      <div
        v-if="open"
        ref="menuRef"
        role="menu"
        class="absolute right-0 top-full z-50 mt-2 w-64 max-w-[calc(100vw-2rem)] rounded-[10px] border border-border bg-surface-card p-1 shadow-xl"
        data-testid="topbar-account-menu"
      >
        <UiText tone="secondary" size="md" class="break-all border-b border-border px-3 py-2 font-medium" data-testid="topbar-account-email">
          {{ email }}
        </UiText>
        <RouterLink
          :to="AppRoute.Settings"
          role="menuitem"
          :class="[itemClasses, 'mt-1 text-text-primary']"
          data-testid="topbar-settings"
          @click="open = false"
        >
          {{ t('nav.settings') }}
        </RouterLink>
        <UiButton
          type="button"
          role="menuitem"
          :class="[itemClasses, 'cursor-pointer text-danger-600 dark:text-danger-400']"
          data-testid="topbar-sign-out"
          @click="signOut"
        >
          {{ t('common.signOut') }}
        </UiButton>
      </div>
    </UiPanelTransition>
  </div>
</template>
