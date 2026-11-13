<template>
  <nav
    class="flex flex-wrap items-center gap-1.5 sm:gap-2 text-xs sm:text-sm text-gray-500 dark:text-gray-400 animate-reveal-up motion-reduce:animate-none"
    :class="containerClass"
    :aria-label="$t('common.breadcrumb')"
  >
    <NuxtLink
      :to="localePath('/')"
      :class="[LINK_CLASSES, 'whitespace-nowrap']"
    >
      {{ $t('common.home') }}
    </NuxtLink>

    <template v-for="(item, index) in items" :key="index">
      <ChevronRight :size="14" :stroke-width="2" class="flex-shrink-0" />

      <NuxtLink
        v-if="item.to && index < items.length - 1"
        :to="item.to"
        :class="[LINK_CLASSES, 'break-words max-w-[150px] sm:max-w-none truncate sm:whitespace-normal']"
      >
        {{ item.label }}
      </NuxtLink>

      <span
        v-else
        :aria-current="index === items.length - 1 ? 'page' : undefined"
        :class="[
          index === items.length - 1 ? 'text-gray-900 dark:text-white font-medium' : '',
          'break-words max-w-[150px] sm:max-w-none truncate sm:whitespace-normal'
        ]"
      >
        {{ item.label }}
      </span>
    </template>
  </nav>
</template>

<script setup lang="ts">
/**
 * UiBreadcrumbs. The trail above a page: Home, then the given items; the last one is the
 * current page and is not a link.
 */
import { ChevronRight } from 'lucide-vue-next'
import { FOCUS_RING } from '~/utils/ui/tones'

/** One step of the trail. */
interface BreadcrumbItem {
  /** Text shown. */
  label: string
  /** Route to link to; omit for a step that is only text. */
  to?: string
}

/** Props of UiBreadcrumbs. */
interface Props {
  /** The steps after Home. */
  items: BreadcrumbItem[]
  /** Extra classes of the nav element (default adds bottom margin). */
  containerClass?: string
}

withDefaults(defineProps<Props>(), {
  containerClass: 'mb-8',
})

/** Look of a step that is a link: hover colour, transition and the shared focus ring. */
const LINK_CLASSES = `rounded-sm transition-colors hover:text-primary-600 dark:hover:text-primary-400 ${FOCUS_RING}`

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()
</script>
