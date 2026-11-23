<script setup lang="ts">
/**
 * Detail/config page for a single integration.
 *
 * Route: /integrations/:slug
 * Layout: the page header (the integration name, with a way back to the list) + the shared config panel (2/3 config form + 1/3 status sidebar).
 */
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import IntegrationConfigPanel from '@/components/integrations/IntegrationConfigPanel.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { AppRoute } from '@/lib/routes'
import { useIntegrationsStore } from '@/stores/integrations'

/** Translation function. */
const { t } = useI18n()

/** Integrations store: the integration the panel below loads names the page. */
const store = useIntegrationsStore()

/** Current route, whose param is the slug. */
const route = useRoute()

/** Slug from the current route. */
const slug = route.params.slug as string
</script>

<template>
  <UiStack gap="md" class="w-full">
    <UiPageHeader
      :title="store.current?.name"
      :loading="store.current === null"
      :back-to="AppRoute.Integrations"
      :back-label="t('nav.integrations')"
    />
    <IntegrationConfigPanel :slug="slug" />
  </UiStack>
</template>
