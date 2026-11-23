<template>
  <NuxtLink
    :to="item.to"
    class="group flex min-h-11 items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-all duration-200"
    :class="active
      ? 'border border-primary-500/20 bg-primary-500/10 text-primary-600 dark:text-primary-400'
      : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-white/5 dark:hover:text-white'"
    :data-testid="testid"
    @click="$emit('navigate')"
  >
    <component
      :is="item.icon"
      :size="18"
      :stroke-width="2"
      :class="active
        ? 'shrink-0 text-primary-600 dark:text-primary-400'
        : 'shrink-0 text-gray-500 group-hover:text-gray-700 dark:group-hover:text-gray-300'"
    />
    <UiText :as="UiTextTag.Span" size="sm" tone="inherit" weight="medium" class="min-w-0 break-words">{{ item.label }}</UiText>
  </NuxtLink>
</template>

<script setup lang="ts">
/**
 * One link of the client sidebar: icon and label, highlighted when it is the current section.
 */
import type { ClientNavItem } from '~/types/clientnavitem'
import { UiTextTag } from '~/utils/ui/enums'

/** Props of ClientLayoutNavItem. */
interface Props {
  /** The link to render. */
  item: ClientNavItem
  /** Whether the current route is inside this link's section. */
  active: boolean
}

/** Props of the component with their defaults applied. */
const props = defineProps<Props>()

/** Events of ClientLayoutNavItem. */
defineEmits<{
  /** The link was followed, so a mobile drawer can close. */
  navigate: []
}>()

/** `data-testid` derived from the target: `/client/domains` becomes `nav-client-domains`. */
const testid = computed(() => `nav-${props.item.to.replace(/^\//, '').replace(/\//g, '-')}`)
</script>
