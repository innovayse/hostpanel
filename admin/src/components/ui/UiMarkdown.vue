<script setup lang="ts">
/**
 * Draws Markdown text that comes from outside the panel, such as a plugin's release notes.
 *
 * The text is read by `parseMarkdown` into a typed tree and drawn with ordinary elements and text
 * nodes: there is no `v-html`, a raw tag in the source stays visible as text, and a link is kept only
 * when it is an https address. When the text is null or blank the `empty` text is shown instead, or
 * nothing when none is given. Headings are drawn as bold lines, never as page headings, so the page
 * outline of the screen that embeds it stays intact.
 */
import { computed } from 'vue'
import UiMarkdownInline from '@/components/ui/UiMarkdownInline.vue'
import UiText from '@/components/ui/UiText.vue'
import { MarkdownBlockKind, parseMarkdown } from '@/utils/markdown'

/** Props for UiMarkdown. */
const props = withDefaults(defineProps<{
  /** The Markdown source; null or blank shows the empty text. */
  source: string | null
  /** Text shown when there is no source, already translated; omit to draw nothing. */
  empty?: string
}>(), {
  empty: undefined,
})

/** The parsed blocks of the source. */
const blocks = computed(() => parseMarkdown(props.source))
</script>

<template>
  <div v-if="blocks.length > 0" class="flex flex-col gap-2 text-sm text-text-secondary" data-testid="markdown">
    <template v-for="block in blocks" :key="block.id">
      <p v-if="block.kind === MarkdownBlockKind.Heading" class="font-semibold text-text-primary">
        <UiMarkdownInline :nodes="block.inlines" />
      </p>
      <p v-else-if="block.kind === MarkdownBlockKind.Paragraph" class="break-words">
        <UiMarkdownInline :nodes="block.inlines" />
      </p>
      <ol v-else-if="block.kind === MarkdownBlockKind.List && block.ordered" class="list-decimal space-y-1 pl-5">
        <li v-for="item in block.items" :key="item.id" class="break-words"><UiMarkdownInline :nodes="item.inlines" /></li>
      </ol>
      <ul v-else-if="block.kind === MarkdownBlockKind.List" class="list-disc space-y-1 pl-5">
        <li v-for="item in block.items" :key="item.id" class="break-words"><UiMarkdownInline :nodes="item.inlines" /></li>
      </ul>
      <blockquote v-else-if="block.kind === MarkdownBlockKind.Quote" class="border-l-2 border-border pl-3 italic">
        <UiMarkdownInline :nodes="block.inlines" />
      </blockquote>
      <pre
        v-else-if="block.kind === MarkdownBlockKind.Code"
        class="overflow-x-auto whitespace-pre rounded-md border border-border bg-surface-field px-3 py-2 font-mono text-xs text-text-primary"
      ><code>{{ block.code }}</code></pre>
      <hr v-else-if="block.kind === MarkdownBlockKind.Rule" class="border-border">
    </template>
  </div>
  <UiText v-else-if="empty !== undefined" tone="muted" size="sm" data-testid="markdown-empty">{{ empty }}</UiText>
</template>
