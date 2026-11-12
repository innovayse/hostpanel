<script setup lang="ts">
/**
 * The inline pieces of one Markdown block: text, bold, italic, code and https links. Every piece is
 * drawn as a text node inside an ordinary element, so nothing from the source can become markup. A
 * link opens in a new tab with the safe `rel`; with `links` off it stays its text. With `caret` on, a
 * blinking caret ends the run, for a reply still being written. Used by {@link UiMarkdown}; it holds no state.
 */
import { MarkdownInlineKind } from '@/utils/markdown'
import type { MarkdownInline } from '@/utils/markdown'

/** Props for UiMarkdownInline. */
withDefaults(defineProps<{
  /** The pieces to draw, in order. */
  nodes: MarkdownInline[]
  /** Whether a link is drawn as a link; off draws its text only. */
  links?: boolean
  /** Ends the run with a blinking caret, as a reply that is still being written does. */
  caret?: boolean
}>(), {
  links: true,
  caret: false,
})
</script>

<template>
  <template v-for="node in nodes" :key="node.id">
    <strong v-if="node.kind === MarkdownInlineKind.Strong" class="font-semibold text-text-primary">{{ node.text }}</strong>
    <em v-else-if="node.kind === MarkdownInlineKind.Emphasis">{{ node.text }}</em>
    <code
      v-else-if="node.kind === MarkdownInlineKind.Code"
      class="rounded bg-surface-field px-1 py-0.5 font-mono text-[0.85em] text-text-primary"
    >{{ node.text }}</code>
    <a
      v-else-if="links && node.kind === MarkdownInlineKind.Link && node.href !== null"
      :href="node.href"
      target="_blank"
      rel="noopener noreferrer nofollow"
      class="text-primary-600 underline underline-offset-2 hover:text-primary-500"
    >{{ node.text }}</a>
    <template v-else>{{ node.text }}</template>
  </template>
  <span
    v-if="caret"
    class="ml-0.5 inline-block h-4 w-0.5 translate-y-0.5 animate-pulse bg-current motion-reduce:animate-none"
    aria-hidden="true"
    data-testid="markdown-caret"
  />
</template>
