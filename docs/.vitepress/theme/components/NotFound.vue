<script setup lang="ts">
/**
 * NotFound: the documentation's own 404 page. It replaces the default theme page through the
 * `not-found` slot of the layout (see `theme/index.ts`) and keeps to the theme: the VitePress colour
 * variables follow light and dark, and the big number uses the same brand gradient as the home page
 * hero. It offers the three ways on: the start of the documentation, the getting started guide and
 * the API reference. The page is marked `noindex` by `transformHead` in `seo.ts`.
 */
import { withBase } from 'vitepress'
import { computed } from 'vue'
import { localizeLink } from '../../i18n/locales'
import { useUi } from '../useUi'

/** One link under the message. */
interface Way {
  /** The label of the link. */
  text: string
  /** The address, relative to the documentation root, in the language of the page. */
  link: string
  /** The button style of the default theme: `brand` for the main way, `alt` for the others. */
  theme: 'brand' | 'alt'
}

/** The texts and the language of the open page; the 404 page is shown in the language of the address that was not found. */
const { ui, locale } = useUi()

/** The ways on, the main one first. */
const ways = computed<Way[]>(() => [
  { text: ui.value.components.notFound.back, link: localizeLink(locale.value, '/'), theme: 'brand' },
  { text: ui.value.components.notFound.getStarted, link: localizeLink(locale.value, '/get-started/'), theme: 'alt' },
  { text: ui.value.components.notFound.apiReference, link: localizeLink(locale.value, '/api-reference/'), theme: 'alt' },
])
</script>

<template>
  <div class="not-found" data-testid="not-found">
    <p class="not-found__label">{{ ui.components.notFound.label }}</p>
    <p class="not-found__code" aria-hidden="true">404</p>
    <h1 class="not-found__title">{{ ui.components.notFound.title }}</h1>
    <p class="not-found__body">{{ ui.components.notFound.body }}</p>
    <div class="not-found__actions">
      <a
        v-for="way in ways"
        :key="way.link"
        :class="['VPButton', 'medium', way.theme]"
        :href="withBase(way.link)"
      >
        {{ way.text }}
      </a>
    </div>
  </div>
</template>

<style scoped>
.not-found {
  max-width: 640px;
  margin: 0 auto;
  padding: 96px 24px 128px;
}

.not-found__label {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--vp-c-brand-1);
}

.not-found__code {
  margin: 12px 0 0;
  font-size: clamp(80px, 22vw, 136px);
  font-weight: 700;
  line-height: 1;
  letter-spacing: -0.03em;
  background: linear-gradient(120deg, #5e8ef3 30%, #a78bfa);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.not-found__title {
  margin: 16px 0 0;
  border-top: 0;
  padding-top: 0;
  font-size: 28px;
  line-height: 1.25;
  color: var(--vp-c-text-1);
}

.not-found__body {
  margin: 12px 0 0;
  font-size: 16px;
  line-height: 1.6;
  color: var(--vp-c-text-2);
}

.not-found__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 28px;
}
</style>
