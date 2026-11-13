<template>
  <UiStack ref="rootRef" gap="none" class="relative" :data-testid="`header-nav-${item.key}`" @focusout="onFocusOut">
    <UiButton
      variant="bare"
      :class="[HEADER_NAV_ITEM, active ? HEADER_NAV_ACTIVE : HEADER_NAV_IDLE, open ? 'bg-classic-surface-2' : '']"
      :aria-expanded="open"
      aria-haspopup="menu"
      :aria-controls="panelId"
      :data-testid="`header-nav-${item.key}-trigger`"
      @click="toggle"
      @keydown.esc="close"
    >
      {{ item.label }}
      <ChevronDown
        :size="14"
        :stroke-width="2"
        class="transition-transform duration-200"
        :class="open ? 'rotate-180' : ''"
        aria-hidden="true"
      />
    </UiButton>

    <UiStack
      v-if="open"
      :id="panelId"
      gap="none"
      role="menu"
      class="absolute left-0 top-full z-50 mt-2 w-72 rounded-lg border border-classic-border bg-classic-surface p-1.5 shadow-lg"
      :data-testid="`header-nav-${item.key}-panel`"
      @keydown.esc="closeAndFocus"
    >
      <UiLink
        v-for="child in item.children"
        :key="child.key"
        :to="child.to"
        tone="inherit"
        :underline="false"
        role="menuitem"
        :class="[HEADER_ICON_ROW, isHeaderPathActive(path, child.to) ? 'bg-classic-brand-soft' : '']"
        :aria-current="isHeaderPathActive(path, child.to) ? 'page' : undefined"
        :testid="`header-nav-child-${child.key}`"
        @click="close"
      >
        <UiIconTile
          :icon="child.icon"
          :tone="isHeaderPathActive(path, child.to) ? 'solid' : 'soft'"
          :data-testid="`header-nav-child-${child.key}-icon`"
        />
        <UiStack gap="none" as="span" class="min-w-0">
          <UiText
            as="span"
            size="sm"
            weight="medium"
            tone="inherit"
            :class="isHeaderPathActive(path, child.to) ? 'text-classic-brand' : 'text-classic-ink'"
          >
            {{ child.label }}
          </UiText>
          <UiText v-if="child.desc" as="span" size="xs" tone="inherit" truncate class="mt-0.5 text-classic-muted">
            {{ child.desc }}
          </UiText>
        </UiStack>
      </UiLink>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * LayoutHeaderDropdown: a classic header entry that opens a panel of links (Hosting, Support).
 *
 * Opens and closes on click, so it works the same for a mouse, a finger and the keyboard. The
 * panel closes on Escape (focus returns to the trigger), on a click outside, when focus leaves
 * it, and when a link is chosen. The trigger reads as current when any of its links is the page
 * being viewed. Every row leads with an icon tile (`child.icon`), so the Hosting and Support
 * entries read at a glance.
 */
import { ChevronDown } from 'lucide-vue-next'
import type { ComponentPublicInstance } from 'vue'
import type { HeaderNavItem } from '~/types/headernavitem'
import { HEADER_ICON_ROW, HEADER_NAV_ACTIVE, HEADER_NAV_IDLE, HEADER_NAV_ITEM } from '~/utils/headerControl'
import { isHeaderPathActive } from '~/utils/headerNav'

/** Props of LayoutHeaderDropdown. */
const props = defineProps<{
  /** The group to draw; its `children` are the links of the panel. */
  item: HeaderNavItem
  /** Path of the page being viewed, to mark the current link. */
  path: string
  /** Whether this group is the header's one active entry; decided by the parent across all entries. */
  active: boolean
}>()

/** Id of the panel, referenced by the trigger's `aria-controls`. */
const panelId = computed(() => `header-nav-panel-${props.item.key}`)

/** Whether the panel is showing. */
const open = ref(false)

/** The root element: outside clicks and focus leaving are measured against it. */
const rootRef = ref<ComponentPublicInstance | null>(null)

/** Opens a closed panel and closes an open one. */
const toggle = (): void => {
  open.value = !open.value
}

/** Closes the panel. */
const close = (): void => {
  open.value = false
}

/** Closes the panel and puts focus back on the trigger, so a keyboard user keeps their place. */
const closeAndFocus = (): void => {
  open.value = false
  const root = rootRef.value?.$el as HTMLElement | undefined
  root?.querySelector<HTMLElement>('button')?.focus()
}

/**
 * Closes the panel when a click lands outside the component (the same rule as the kit's popover).
 *
 * @param event - The document click.
 */
const onDocumentClick = (event: MouseEvent): void => {
  const root = rootRef.value?.$el as HTMLElement | undefined
  if (open.value && root && !root.contains(event.target as Node)) { // a document click's target is always a Node
    close()
  }
}

onMounted(() => {
  document.addEventListener('click', onDocumentClick)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onDocumentClick)
})

/**
 * Closes the panel when keyboard focus moves to something outside the dropdown.
 *
 * @param event - The focus-out event of the root.
 */
const onFocusOut = (event: FocusEvent): void => {
  const root = rootRef.value?.$el as HTMLElement | undefined
  const next = event.relatedTarget as Node | null // null when focus leaves the window
  if (next && root && !root.contains(next)) {
    close()
  }
}
</script>
