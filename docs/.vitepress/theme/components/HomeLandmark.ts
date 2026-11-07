/**
 * Draws nothing. Once the page is on screen, and again after every change of page, it marks the home
 * layout as the main landmark (see `landmark.ts`). Rendered in the `layout-bottom` slot of the theme.
 */
import { useData } from 'vitepress'
import { defineComponent, onMounted, watch } from 'vue'
import { markHomeAsMain } from '../landmark'

export default defineComponent({
  name: 'HomeLandmark',
  setup() {
    const { frontmatter } = useData()

    /** Marks the home content when the page on screen is the home page. */
    const apply = (): void => {
      markHomeAsMain(document, frontmatter.value.layout === 'home')
    }

    onMounted(apply)
    // After the DOM of the new page is in place, so the home element exists when it is looked up.
    watch(() => frontmatter.value.layout, apply, { flush: 'post' })

    return () => null
  },
})
