<script setup lang="ts">
/**
 * Screenshot — renders a thumbnail that opens a full-size lightbox overlay on
 * click or Enter/Space. The overlay is Teleported to `<body>` after mount to
 * avoid a hydration mismatch during SSR.
 */
import { ref, onMounted, onBeforeUnmount } from 'vue'

/** Props accepted by the Screenshot component. */
const props = withDefaults(
  defineProps<{
    /** Path to the image, relative to the VitePress `public/` directory. */
    src: string
    /** Accessible description of the image content. */
    alt?: string
  }>(),
  { alt: '' },
)

/** Whether the full-size lightbox overlay is currently open. */
const open = ref(false)

/**
 * True once the component is mounted in the browser.
 * The Teleport target (`body`) does not exist during SSR, so the overlay is
 * rendered only after mount to avoid a hydration mismatch.
 */
const isMounted = ref(false)

/** Opens the full-size lightbox overlay. */
const openLightbox = (): void => {
  open.value = true
}

/** Closes the full-size lightbox overlay. */
const closeLightbox = (): void => {
  open.value = false
}

/**
 * Closes the overlay when the user presses Escape.
 * @param e - The keyboard event fired on the window.
 */
const handleKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') { closeLightbox() }
}

onMounted(() => {
  isMounted.value = true
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
