<script lang="ts">
/**
 * UiMarkdownText - shows a text written in the small Markdown subset of `utils/ui/markdownText.ts`, such as an AI reply.
 *
 * The string is never injected as markup: it is read into a tree of known tags and drawn with `h()`, so all of its text
 * is escaped by Vue and HTML in it stays visible text.
 */
import { defineComponent, h } from 'vue'
import type { PropType } from 'vue'
import { MARKDOWN_TAG_CLASSES, parseMarkdownText } from '~/utils/ui/markdownText'
import { renderRichNode } from '~/utils/ui/richText'

export default defineComponent({
  name: 'UiMarkdownText',
  props: {
    /** The text as written. */
    text: { type: String as PropType<string>, default: '' },
  },
  setup(props) {
    return () => h(
      'div',
      { 'data-testid': 'ui-markdown-text', class: 'break-words' },
      parseMarkdownText(props.text).map((node) => renderRichNode(node, MARKDOWN_TAG_CLASSES)),
    )
  },
})
</script>
