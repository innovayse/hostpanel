<template>
  <div class="space-y-2">
    <div
      v-for="(item, index) in items"
      :key="index"
      class="border border-gray-200 rounded-lg overflow-hidden"
    >
      <!-- Accordion header -->
      <button
        type="button"
        class="w-full flex items-center justify-between p-4 text-left hover:bg-gray-50 transition-colors"
        @click="toggle(index)"
      >
        <span class="font-medium text-gray-900">
          {{ item.title }}
        </span>
        <ChevronDown
          :size="20"
          :stroke-width="2"
          class="text-gray-500 transition-transform duration-200"
          :class="{ 'rotate-180': isOpen(index) }"
        />
      </button>

      <!-- Accordion content -->
      <Transition name="accordion">
        <div v-if="isOpen(index)" class="border-t border-gray-200">
          <div class="p-4 text-gray-600">
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
 * Accordion component for collapsible content sections
 * Supports single or multiple open items
 */

import { ChevronDown } from 'lucide-vue-next'

interface AccordionItem {
  /** Item title/header */
  title: string
  /** Item content (can be overridden by slot) */
  content?: string
}

interface Props {
  /** Array of accordion items */
  items: AccordionItem[]
  /** Allow multiple items open at once */
  multiple?: boolean
  /** Default open item indices */
  defaultOpen?: number[]
}

const props = withDefaults(defineProps<Props>(), {
  multiple: false,
  defaultOpen: () => []
})

/** Track which items are open */
const openItems = ref<Set<number>>(new Set(props.defaultOpen))

/** Check if an item is open */
const isOpen = (index: number): boolean => {
  return openItems.value.has(index)
}

/** Toggle an item open/closed */
const toggle = (index: number) => {
  if (props.multiple) {
    // Multiple mode: toggle individual item
    if (openItems.value.has(index)) {
      openItems.value.delete(index)
    } else {
      openItems.value.add(index)
    }
  } else {
    // Single mode: close others, open this one
    if (openItems.value.has(index)) {
      openItems.value.clear()
    } else {
      openItems.value.clear()
      openItems.value.add(index)
    }
  }
}
</script>

<style scoped>
/* Accordion transition animations */
.accordion-enter-active,
.accordion-leave-active {
  transition: all 0.3s ease;
  overflow: hidden;
}

.accordion-enter-from,
.accordion-leave-to {
  max-height: 0;
  opacity: 0;
}

.accordion-enter-to,
.accordion-leave-from {
  max-height: 500px;
  opacity: 1;
}
</style>
