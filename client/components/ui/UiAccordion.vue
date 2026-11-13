<template>
  <div class="space-y-2">
    <div
      v-for="(item, index) in items"
      :key="index"
      class="ui-accordion-item border border-gray-200 dark:border-white/10 rounded-lg overflow-hidden"
    >
      <!-- Accordion header -->
      <button
        type="button"
        :aria-expanded="isOpen(index)"
        :data-testid="`ui-accordion-toggle-${index}`"
        class="w-full flex items-center justify-between p-4 text-left hover:bg-gray-50 dark:hover:bg-white/5 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500"
        @click="toggle(index)"
      >
        <span class="font-medium text-gray-900 dark:text-white">
          {{ item.title }}
        </span>
        <ChevronDown
          :size="20"
          :stroke-width="2"
          class="text-gray-500 dark:text-gray-400 transition-transform duration-200"
          :class="{ 'rotate-180': isOpen(index) }"
        />
      </button>

      <!-- Accordion content -->
      <Transition
        enter-active-class="transition-all duration-300 ease-out overflow-hidden"
        leave-active-class="transition-all duration-300 ease-out overflow-hidden"
        enter-from-class="max-h-0 opacity-0"
        enter-to-class="max-h-[500px] opacity-100"
        leave-from-class="max-h-[500px] opacity-100"
        leave-to-class="max-h-0 opacity-0"
      >
        <div v-if="keepContent || isOpen(index)" v-show="isOpen(index)" class="border-t border-gray-200 dark:border-white/10">
          <div class="p-4 text-gray-600 dark:text-gray-400">
            <slot :name="`content-${index}`" :item="item">
              {{ item.content }}
            </slot>
          </div>
        </div>
      </Transition>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiAccordion: collapsible sections, single-open by default or multi-open with `multiple`.
 *
 * Each section's body defaults to `item.content` and can be replaced through the scoped slot
 * `content-<index>`.
 */
import { ref } from 'vue'
import { ChevronDown } from 'lucide-vue-next'

/** One section of the accordion. */
interface AccordionItem {
  /** Header text. */
  title: string
  /** Body text; the `content-<index>` slot overrides it. */
  content?: string
}

/** Props of UiAccordion. */
interface Props {
  /** The sections. */
  items: AccordionItem[]
  /** Allows several sections open at once. */
  multiple?: boolean
  /** Indices open on first render. */
  defaultOpen?: number[]
  /** Keeps every body in the page, hidden while closed, so crawlers read the answers. */
  keepContent?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  multiple: false,
  defaultOpen: () => [],
  keepContent: false,
})

/** Indices of the sections currently open. */
const openItems = ref<Set<number>>(new Set(props.defaultOpen))

/**
 * Whether a section is open.
 *
 * @param index Section index.
 * @returns True when open.
 */
const isOpen = (index: number): boolean => openItems.value.has(index)

/**
 * Opens a closed section or closes an open one; in single mode it closes the others.
 *
 * @param index Section index.
 */
const toggle = (index: number): void => {
  const wasOpen = openItems.value.has(index)
  if (props.multiple) {
    if (wasOpen) {
      openItems.value.delete(index)
    } else {
      openItems.value.add(index)
    }
    return
  }
  openItems.value.clear()
  if (!wasOpen) {
    openItems.value.add(index)
  }
}
</script>
