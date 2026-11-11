<script setup lang="ts">
/**
 * HomeDemoButton: the "Try the demo" button under the actions of the home page hero.
 *
 * Reads the admin demo address from the theme data (`demoAdminUrl`, set from the build
 * environment) and renders nothing when it is not set, so a build without a demo has no button.
 */
import { useData } from 'vitepress'
import { computed } from 'vue'
import { useUi } from '../useUi'

const { theme } = useData()

/** The texts of the language of the open page. */
const { ui } = useUi()

/** Address of the admin demo, or an empty string when the site has none. */
const demoUrl = computed<string>(() => (theme.value as Record<string, string>).demoAdminUrl ?? '')
</script>

<template>
  <div v-if="demoUrl" class="home-demo-action">
    <a :href="demoUrl" target="_blank" rel="noopener noreferrer" class="VPButton medium alt">
      {{ ui.nav.demo }}
    </a>
  </div>
</template>

<style scoped>
.home-demo-action {
  display: flex;
  justify-content: center;
  margin-top: 12px;
}
</style>
