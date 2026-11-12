<script lang="ts">
/**
 * UiMarkdownText - shows a text written in the small Markdown subset of `utils/ui/markdownText.ts`, such as an AI reply.
 *
 * The string is never injected as markup: it is read into a tree of known tags and drawn with `h()`, so all of its text
 * is escaped by Vue and HTML in it stays visible text. A code block gets a copy button when the caller names the button
 * (`copyLabel`), and a reply still being written ends in a blinking caret (`caret`).
 */
import { defineComponent, h } from 'vue'
import type { PropType } from 'vue'
import UiCopyButton from '~/components/ui/UiCopyButton.vue'
import {
  appendCaret,
  MARKDOWN_COPYABLE_TAG_CLASSES,
  MARKDOWN_COPYABLE_WRAPPER_CLASSES,
  MARKDOWN_TAG_CLASSES,
  parseMarkdownText,
} from '~/utils/ui/markdownText'
import { UiRichNodeType } from '~/utils/ui/enums'
import { renderRichNode } from '~/utils/ui/richText'
import type { RichNode } from '~/utils/ui/richText'

/**
 * The text a code block holds.
 *
 * @param node - A `pre` node of the parsed reply.
 * @returns Its text.
 */
const codeOf = (node: RichNode): string =>
  node.type === UiRichNodeType.Element ? node.children.map(child => (child.type === UiRichNodeType.Text ? child.text : '')).join('') : ''

export default defineComponent({
  name: 'UiMarkdownText',
  props: {
    /** The text as written. */
    text: { type: String as PropType<string>, default: '' },
    /** Accessible name of a code block's copy button; absent, code blocks have no button. */
    copyLabel: { type: String as PropType<string>, default: '' },
    /** Accessible name of that button for a moment after the copy. */
    copiedLabel: { type: String as PropType<string>, default: '' },
    /** Whether the reply is still being written, which ends it in a blinking caret. */
    caret: { type: Boolean as PropType<boolean>, default: false },
  },
  setup(props) {
    return () => {
      const blocks = parseMarkdownText(props.text)
      if (props.caret) {
        appendCaret(blocks)
      }
      return h(
        'div',
        { 'data-testid': 'ui-markdown-text', class: 'break-words' },
        blocks.map((node) => {
          if (props.copyLabel !== '' && node.type === UiRichNodeType.Element && node.tag === 'pre') {
            return h('div', { class: MARKDOWN_COPYABLE_WRAPPER_CLASSES }, [
              renderRichNode(node, MARKDOWN_COPYABLE_TAG_CLASSES),
              h(UiCopyButton, {
                value: codeOf(node),
                label: props.copyLabel,
                copiedLabel: props.copiedLabel || props.copyLabel,
                testid: 'ui-markdown-copy',
                class: 'absolute right-1 top-1',
              }),
            ])
          }
          return renderRichNode(node, MARKDOWN_TAG_CLASSES)
        }),
      )
    }
  },
})
</script>
