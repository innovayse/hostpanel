<template>
  <header class="mb-8" data-testid="ui-page-header">
    <!-- The way back sits on its own row above the title at every width, so it never takes width from it -->
    <div v-if="backTo !== undefined && backLabel !== undefined" class="mb-3">
      <UiBackLink :to="backTo" :label="backLabel" :testid="backTestid" />
    </div>
    <div v-if="$slots.breadcrumbs" class="mb-4">
      <slot name="breadcrumbs" />
    </div>
    <div class="flex flex-wrap items-start justify-between gap-x-4 gap-y-3">
      <div class="flex min-w-0 max-w-full flex-[1_1_16rem] items-start gap-3">
        <div v-if="icon" class="w-11 h-11 rounded-xl bg-primary-500/10 flex items-center justify-center flex-shrink-0">
          <component :is="icon" :size="22" :stroke-width="2" class="text-primary-600 dark:text-primary-400" />
        </div>
        <div class="min-w-0">
          <!-- Smaller on a phone; a long title wraps at word boundaries and breaks only a word that cannot fit -->
          <UiHeading :level="1" :size="UiHeadingSize.Bare" class="break-words text-2xl font-bold text-gray-900 dark:text-white sm:text-3xl md:text-4xl">{{ title }}</UiHeading>
          <UiText v-if="description" tone="muted" class="mt-1">{{ description }}</UiText>
        </div>
      </div>
      <div v-if="$slots.actions || actions.length" class="flex min-w-0 max-w-full flex-shrink-0 flex-wrap items-center gap-3">
        <slot name="actions" />
        <!-- From sm: every action is a button -->
        <UiStack v-if="actions.length" direction="row" align="center" gap="sm" wrap class="hidden sm:flex" data-testid="ui-page-header-buttons">
          <UiButton
            v-for="action in ordered"
            :key="action.key"
            :variant="action.primary ? UiButtonVariant.Primary : UiButtonVariant.Outline"
            :to="action.to"
            :loading="action.loading"
            :data-testid="action.testid"
            @click="run(action)"
          >
            <component :is="action.icon" :size="16" :stroke-width="2" class="mr-2" aria-hidden="true" />
            {{ action.label }}
          </UiButton>
        </UiStack>
        <!-- On a phone: one compact icon button that opens a menu with all of them, the primary one first -->
        <UiPopover v-if="actions.length" v-model="menuOpen" class="sm:hidden" panel-class="w-64">
          <template #trigger="{ toggle }">
            <UiIconButton
              :icon="hasPrimary ? Plus : MoreHorizontal"
              :label="$t('ui.pageHeader.actions')"
              :variant="hasPrimary ? 'primary' : 'subtle'"
              aria-haspopup="menu"
              :aria-expanded="menuOpen"
              testid="ui-page-header-menu"
              @click="toggle"
            />
          </template>
          <template #default="{ close }">
            <UiButton
              v-for="action in ordered"
              :key="action.key"
              variant="ghost"
              role="menuitem"
              full-width
              :to="action.to"
              :loading="action.loading"
              class="!justify-start gap-3"
              :data-testid="`${action.testid}-menu`"
              @click="run(action); close()"
            >
              <component :is="action.icon" :size="16" :stroke-width="2" aria-hidden="true" />
              {{ action.label }}
            </UiButton>
          </template>
        </UiPopover>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
/**
 * UiPageHeader. The top of a page: an optional way back, breadcrumbs, the h1 title with an
 * optional icon and description, and the page's actions on the right.
 *
 * A screen that returns to a parent passes `backTo` and `backLabel`; the back link then sits on
 * its own row above the title at every width. The title is smaller on a phone and wraps at word
 * boundaries. The `actions` prop takes the screen's actions as data: from `sm` up they are
 * buttons (the primary one filled), on a phone they fold into ONE compact icon button, a plus
 * when one of them is the primary create-like action and the overflow icon otherwise, that opens
 * a menu with every action under its full label, primary first. The `actions` slot takes what is
 * not a plain action (a block a plugin supplies) and is drawn at every width.
 */
import { computed, ref } from 'vue'
import type { Component } from 'vue'
import { MoreHorizontal, Plus } from 'lucide-vue-next'
import { UiButtonVariant, UiHeadingSize } from '~/utils/ui/enums'
import type { UiHeaderAction } from '~/types/uiheaderaction'

/** Props of UiPageHeader. */
interface Props {
  /** The page title, rendered as the h1. */
  title: string
  /** One sentence under the title. */
  description?: string
  /** A lucide icon component shown beside the title. */
  icon?: Component
  /** Route the back link returns to; the link shows only together with `backLabel`. */
  backTo?: string
  /** Short, already-translated name of the place the back link returns to. */
  backLabel?: string
  /** `data-testid` of the back link. */
  backTestid?: string
  /** The page's actions; buttons from `sm`, one menu on a phone. */
  actions?: UiHeaderAction[]
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  description: undefined,
  icon: undefined,
  backTo: undefined,
  backLabel: undefined,
  backTestid: 'ui-page-header-back',
  /**
   * Default for the header actions.
   *
   * @returns A new empty list, so instances never share one.
   */
  actions: () => [],
})

/** Whether the phone menu is open. */
const menuOpen = ref(false)

/** The actions with the primary one first, the rest in the order the screen gave. */
const ordered = computed(() => [...props.actions].sort((a, b) => Number(Boolean(b.primary)) - Number(Boolean(a.primary))))

/** Whether one action is the primary create-like one, which turns the phone trigger into a plus. */
const hasPrimary = computed(() => props.actions.some(action => action.primary))

/**
 * Runs an action that has no route; a routed action is followed by its link.
 *
 * @param action The chosen action.
 */
const run = (action: UiHeaderAction): void => {
  if (action.to === undefined) {
    void action.onSelect?.()
  }
}
</script>
