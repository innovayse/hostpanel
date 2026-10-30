<script setup lang="ts">
/**
 * A search prompt rendered on the home page between the features grid and the
 * popular guides section. Clicking the button opens the VitePress local search
 * dialog (the same one bound to Ctrl K / Cmd K in the navigation bar).
 */

/** Opens the VitePress local search overlay programmatically. */
const openSearch = (): void => {
  const btn = document.querySelector<HTMLButtonElement>('.VPLocalSearchBox, button[aria-label*="Search"]')
  if (btn) {
    btn.click()
    return
  }
  // Fallback: dispatch the keyboard shortcut.
  document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true, ctrlKey: true, bubbles: true }))
}
</script>

<template>
  <div class="home-search">
    <button class="home-search-btn" aria-label="Open documentation search" @click="openSearch">
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="home-search-icon" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
      </svg>
      <span class="home-search-placeholder">Search documentation…</span>
      <span class="home-search-shortcut"><kbd>Ctrl</kbd><kbd>K</kbd></span>
    </button>
  </div>
</template>

<style scoped>
.home-search {
  display: flex;
  justify-content: center;
  padding: 32px 24px 8px;
}

.home-search-btn {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  max-width: 480px;
  padding: 10px 16px;
  background: var(--vp-c-bg-soft);
  border: 1px solid var(--vp-c-divider);
  border-radius: 8px;
  color: var(--vp-c-text-2);
  font-size: 0.95rem;
  cursor: pointer;
  transition: border-color 0.2s ease, background 0.2s ease;
  text-align: left;
}

.home-search-btn:hover,
.home-search-btn:focus-visible {
  border-color: var(--vp-c-brand-1);
  background: var(--vp-c-bg-elv);
  outline: none;
}

.home-search-icon {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

.home-search-placeholder {
  flex: 1;
}

.home-search-shortcut {
  display: flex;
  gap: 4px;
  font-size: 0.78rem;
}

.home-search-shortcut kbd {
  display: inline-block;
  padding: 2px 5px;
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 4px;
  font-family: inherit;
  line-height: 1.4;
}
</style>
