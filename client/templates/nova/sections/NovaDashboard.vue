<template>
  <UiSection v-if="!compact" id="dashboard" padding="lg" class="border-b border-nova-border">
    <UiSplit gap="xl" class="items-center">
      <template #start>
        <NovaSectionHeader
          :eyebrow="t('nova.dashboard.eyebrow')"
          :title="t('nova.dashboard.title')"
          :lead="t('nova.dashboard.lead')"
        />
        <NovaCta
          :to="localePath(CLIENT_LOGIN_PATH)"
          tone="outline"
          class="mt-7"
          data-testid="nova-dashboard-cta"
        >
          {{ t('nova.dashboard.cta') }}
        </NovaCta>
      </template>
      <template #end>
        <UiStack gap="none" class="n-reveal">
          <NovaDashboardWindow />
        </UiStack>
      </template>
    </UiSplit>
  </UiSection>

  <UiStack v-else gap="none" class="n-reveal min-w-0">
    <NovaDashboardWindow />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * nova control-panel preview.
 *
 * Drawn in CSS rather than shipped as an image. No screenshot of this panel
 * exists in the repository, and a stock dashboard image would be a picture of
 * software the visitor is not being sold, so the preview names the areas the
 * client area really has and shows nothing it cannot name.
 *
 * `compact` drops the copy and the section wrapper, which is how the hero
 * reuses the same window without a second copy of the markup or a second h2.
 */
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import NovaCta from '~/templates/nova/sections/NovaCta.vue'
import NovaDashboardWindow from '~/templates/nova/sections/DashboardWindow.vue'
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'

/** Props of NovaDashboard. */
withDefaults(defineProps<{
  /** Render the window alone, for use inside another section. */
  compact?: boolean
}>(), { compact: false })

/** Translator for the copy. */
const { t } = useI18n()
/** Prefixes a route with the active locale. */
const localePath = useLocalePath()
</script>
