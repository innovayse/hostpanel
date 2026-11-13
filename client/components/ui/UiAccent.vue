<script lang="ts">
/**
 * UiAccent: scopes a data colour (a product's or a slide's brand colour) to everything inside it.
 *
 * The colour is written once, as the `--accent` custom property of the wrapper, and the kit's
 * accent classes (`utils/ui/accent.ts`) read it, so a screen never carries a hex or an inline
 * style of its own. The wrapper is `display: contents`, so it takes no part in the layout: its
 * children are laid out as if it were not there. The value is checked first (`isAccentColor`); a
 * colour that is not a plain hex is dropped and the children fall back to their neutral look.
 * It is a render function, not a template, so the property is part of the server-rendered markup
 * and the colour is right before hydration. This is the one place in the portal where a style
 * attribute is produced, because the value is data that no stylesheet can know in advance.
 */
import { defineComponent, h } from 'vue'
import { ACCENT_INK_VARIABLE, ACCENT_VARIABLE, accentInk, isAccentColor } from '~/utils/ui/accent'

export default defineComponent({
  name: 'UiAccent',
  props: {
    /** The colour as a hex string, e.g. `#0ea5e9`. */
    color: { type: String, default: undefined },
    /** Tag of the wrapper element. */
    as: { type: String, default: 'div' },
  },
  setup(props, { slots }) {
    return () => h(
      props.as,
      {
        class: 'contents',
        'data-testid': 'ui-accent',
        style: isAccentColor(props.color)
          ? { [ACCENT_VARIABLE]: props.color, [ACCENT_INK_VARIABLE]: accentInk(props.color) }
          : undefined,
      },
      slots.default?.(),
    )
  },
})
</script>
