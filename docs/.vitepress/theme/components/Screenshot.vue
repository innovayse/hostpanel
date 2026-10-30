<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'

const props = withDefaults(
  defineProps<{ src: string; alt?: string }>(),
  { alt: '' },
)

const open = ref(false)
const isMounted = ref(false)

onMounted(() => {
  isMounted.value = true
})

function openLightbox() {
  open.value = true
}

function closeLightbox() {
  open.value = false
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') closeLightbox()
}

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <span class="screenshot-wrapper">
    <img
      :src="props.src"
      :alt="props.alt"
      class="screenshot-thumb"
      role="button"
      tabindex="0"
      :aria-label="`Enlarge image${props.alt ? ': ' + props.alt : ''}`"
      @click="openLightbox"
      @keydown.enter="openLightbox"
      @keydown.space.prevent="openLightbox"
    />

    <Teleport v-if="isMounted" to="body">
      <div
        v-if="open"
        class="screenshot-overlay"
        role="dialog"
        aria-modal="true"
        :aria-label="props.alt || 'Image preview'"
        @click="closeLightbox"
      >
        <button
          class="screenshot-close"
          aria-label="Close image preview"
          @click.stop="closeLightbox"
        >
          &#x2715;
        </button>
        <img
          :src="props.src"
          :alt="props.alt"
          class="screenshot-full"
          @click.stop
        />
      </div>
    </Teleport>
  </span>
</template>

<style scoped>
.screenshot-wrapper {
  display: block;
  margin: 16px 0;
}

.screenshot-thumb {
  display: block;
  max-width: 100%;
  border: 1px solid var(--vp-c-divider);
  border-radius: 8px;
  cursor: zoom-in;
  transition: box-shadow 0.15s ease, border-color 0.15s ease;
}

.screenshot-thumb:hover,
.screenshot-thumb:focus-visible {
  outline: none;
  border-color: var(--vp-c-brand-1);
  box-shadow: 0 0 0 3px var(--vp-c-brand-soft);
}

.screenshot-overlay {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: rgba(0, 0, 0, 0.85);
  cursor: zoom-out;
  padding: 24px;
}

.screenshot-full {
  max-width: 100%;
  max-height: 100%;
  border-radius: 6px;
  box-shadow: 0 8px 48px rgba(0, 0, 0, 0.6);
  cursor: default;
  object-fit: contain;
}

.screenshot-close {
  position: absolute;
  top: 16px;
  right: 20px;
  background: none;
  border: none;
  color: #fff;
  font-size: 1.5rem;
  line-height: 1;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  opacity: 0.75;
  transition: opacity 0.15s ease;
}

.screenshot-close:hover,
.screenshot-close:focus-visible {
  opacity: 1;
  outline: 2px solid rgba(255, 255, 255, 0.5);
  outline-offset: 2px;
}
</style>
