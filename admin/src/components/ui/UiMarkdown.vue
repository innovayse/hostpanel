<script setup lang="ts">
/**
 * Draws Markdown text that comes from outside the panel, such as a plugin's release notes or an assistant's reply.
 *
 * The text is read by `parseMarkdown` into a typed tree and drawn with ordinary elements and text
 * nodes: there is no `v-html`, a raw tag in the source stays visible as text, and a link is kept only
 * when it is an https address. When the text is null or blank the `empty` text is shown instead, or
 * nothing when none is given. Headings are drawn as bold lines, never as page headings, so the page
 * outline of the screen that embeds it stays intact. A code block gets a copy button when the caller names
 * the button (`copyLabel`), and a reply still being written ends in a blinking caret (`caret`).
 */
import { computed } from 'vue'
import UiCopyButton from '@/components/ui/UiCopyButton.vue'
import UiMarkdownInline from '@/components/ui/UiMarkdownInline.vue'
import UiText from '@/components/ui/UiText.vue'
import { MarkdownBlockKind, parseMarkdown } from '@/utils/markdown'
import type { MarkdownBlock, MarkdownListItem } from '@/utils/markdown'

/** Props for UiMarkdown. */
const props = withDefaults(defineProps<{
  /** The Markdown source; null or blank shows the empty text. */
  source: string | null
  /** Text shown when there is no source, already translated; omit to draw nothing. */
  empty?: string
  /**
   * Whether an https link of the source is drawn as a link. Off for text whose links must pass a stricter rule
   * of the caller's own (a plugin's reply links only to the plugin's allowed origins), so a link stays its text.
   */
  links?: boolean
  /** Accessible name of a code block's copy button, already translated; absent, code blocks have no button. */
  copyLabel?: string
  /** Accessible name of that button for a moment after the copy; absent, the copy label stays. */
  copiedLabel?: string
  /** Whether the text is still being written, which ends it in a blinking caret. */
  caret?: boolean
}>(), {
  empty: undefined,
  links: true,
  copyLabel: undefined,
  copiedLabel: undefined,
  caret: false,
})

/** The parsed blocks of the source. */
const blocks = computed(() => parseMarkdown(props.source))

/** The block kinds that end in a line of text, where the caret can sit. */
const TEXT_BLOCK_KINDS: ReadonlySet<MarkdownBlockKind> = new Set([
  MarkdownBlockKind.Heading,
  MarkdownBlockKind.Paragraph,
  MarkdownBlockKind.List,
  MarkdownBlockKind.Quote,
])

/** The block the caret ends: the last one, when it ends in text; none otherwise. */
const caretBlock = computed<MarkdownBlock | null>(() => {
  const last = blocks.value.at(-1)
  return props.caret && last && TEXT_BLOCK_KINDS.has(last.kind) ? last : null
})

/** Whether the caret stands on a line of its own: after a code block, a rule, or a text with no block yet. */
const caretAlone = computed<boolean>(() => props.caret && caretBlock.value === null)

/** The name of the copy button after a copy. */
const copiedName = computed<string | undefined>(() => props.copiedLabel ?? props.copyLabel)

/**
 * Whether the caret ends a block of running text.
 *
 * @param block - The block.
 * @returns True for the block the caret ends, unless it is a list, whose last item takes it.
 */
const caretOnBlock = (block: MarkdownBlock): boolean => caretBlock.value?.id === block.id && block.kind !== MarkdownBlockKind.List

/**
 * Whether the caret ends an item of a list.
 *
 * @param block - The list.
 * @param item - The item.
 * @returns True for the last item of the list the caret ends.
 */
const caretOnItem = (block: MarkdownBlock, item: MarkdownListItem): boolean =>
  caretBlock.value?.id === block.id && block.items.at(-1)?.id === item.id
</script>

<template>
  <div v-if="blocks.length > 0 || caret" class="flex flex-col gap-2 text-sm text-text-secondary" data-testid="markdown">
    <template v-for="block in blocks" :key="block.id">
      <p v-if="block.kind === MarkdownBlockKind.Heading" class="font-semibold text-text-primary">
        <UiMarkdownInline :nodes="block.inlines" :links="links" :caret="caretOnBlock(block)" />
      </p>
      <p v-else-if="block.kind === MarkdownBlockKind.Paragraph" class="break-words">
        <UiMarkdownInline :nodes="block.inlines" :links="links" :caret="caretOnBlock(block)" />
      </p>
      <ol v-else-if="block.kind === MarkdownBlockKind.List && block.ordered" class="list-decimal space-y-1 pl-5">
        <li v-for="item in block.items" :key="item.id" class="break-words"><UiMarkdownInline :nodes="item.inlines" :links="links" :caret="caretOnItem(block, item)" /></li>
      </ol>
      <ul v-else-if="block.kind === MarkdownBlockKind.List" class="list-disc space-y-1 pl-5">
        <li v-for="item in block.items" :key="item.id" class="break-words"><UiMarkdownInline :nodes="item.inlines" :links="links" :caret="caretOnItem(block, item)" /></li>
      </ul>
      <blockquote v-else-if="block.kind === MarkdownBlockKind.Quote" class="border-l-2 border-border pl-3 italic">
        <UiMarkdownInline :nodes="block.inlines" :links="links" :caret="caretOnBlock(block)" />
      </blockquote>
      <div v-else-if="block.kind === MarkdownBlockKind.Code" class="relative">
        <pre
          class="overflow-x-auto whitespace-pre rounded-md border border-border bg-surface-field px-3 py-2 font-mono text-xs text-text-primary"
          :class="copyLabel ? 'pr-10' : ''"
        ><code>{{ block.code }}</code></pre>
        <UiCopyButton
          v-if="copyLabel"
          :value="block.code"
          :label="copyLabel"
          :copied-label="copiedName ?? copyLabel"
          class="absolute right-1 top-1"
          testid="markdown-copy"
        />
      </div>
      <hr v-else-if="block.kind === MarkdownBlockKind.Rule" class="border-border">
    </template>
    <p v-if="caretAlone"><UiMarkdownInline :nodes="[]" caret /></p>
  </div>
  <UiText v-else-if="empty !== undefined" tone="muted" size="sm" data-testid="markdown-empty">{{ empty }}</UiText>
</template>
