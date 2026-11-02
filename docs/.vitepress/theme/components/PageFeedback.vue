<script setup lang="ts">
/**
 * "Was this page helpful?" widget shown at the bottom of every page.
 * A yes/no opens a pre-filled GitHub issue so no data is stored on our side.
 */
import { ref } from 'vue'
import { useData } from 'vitepress'

const { page } = useData()

/** Whether the user already gave feedback on this page. */
const voted = ref(false)

/**
 * Opens a pre-filled GitHub issue for the given sentiment.
 *
 * @param helpful - Whether the user found the page helpful.
 */
const vote = (helpful: boolean): void => {
  voted.value = true
  const title = encodeURIComponent(`Docs feedback: ${page.value.title}`)
  const body = encodeURIComponent(
    `Page: ${page.value.filePath}\nHelpful: ${helpful ? 'yes' : 'no'}\n\n<!-- Add your comment here -->`,
  )
  window.open(
    `https://github.com/innovayse/hostpanel/issues/new?title=${title}&body=${body}&labels=documentation`,
    '_blank',
    'noopener',
  )
}
</script>

<template>
  <div class="page-feedback">
    <template v-if="!voted">
      <span class="page-feedback__label">Was this page helpful?</span>
      <button class="page-feedback__btn" @click="vote(true)">Yes</button>
      <button class="page-feedback__btn" @click="vote(false)">No</button>
    </template>
    <span v-else class="page-feedback__thanks">Thanks for the feedback!</span>
  </div>
</template>

<style scoped>
.page-feedback {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 2rem;
  padding-top: 1.5rem;
  border-top: 1px solid var(--vp-c-divider);
  font-size: 0.875rem;
  color: var(--vp-c-text-2);
}

.page-feedback__btn {
  padding: 0.25rem 0.75rem;
  border: 1px solid var(--vp-c-divider);
  border-radius: 6px;
  background: transparent;
  color: var(--vp-c-text-1);
  cursor: pointer;
  font-size: 0.875rem;
  transition: border-color 0.2s, background 0.2s;
}

.page-feedback__btn:hover {
  border-color: var(--vp-c-brand-1);
  color: var(--vp-c-brand-1);
}
</style>
