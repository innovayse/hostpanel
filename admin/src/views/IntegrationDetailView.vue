<script setup lang="ts">
/**
 * Detail/config page for a single integration.
 *
 * Route: /integrations/:slug
 * Layout: the page header (the integration name, with its breadcrumb trail above it) + the shared config panel
 * (2/3 config form + 1/3 status sidebar).
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import IntegrationConfigPanel from '@/components/integrations/IntegrationConfigPanel.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { usePageCrumbs } from '@/composables/usePageCrumbs'
import { useIntegrationsStore } from '@/stores/integrations'
import { integrationLabelText } from '@/types/integrationmeta'
import { UiGap } from '@/utils/ui/layout'

/** Builds the trail the header draws above the title: Integrations, the integration. */
const { crumbs } = usePageCrumbs()

/** Integrations store: the integration the panel below loads names the page. */
const store = useIntegrationsStore()

/** The loaded integration's name in the operator's current language; undefined while it loads. */
const name = computed<string | undefined>(() => (store.current ? integrationLabelText(store.current.name) : undefined))

/** Current route, whose param is the slug. */
const route = useRoute()

/** Slug from the current route. */
const slug = route.params.slug as string
</script>

<template>
  <UiStack :gap="UiGap.Md" class="w-full">
    <UiPageHeader
      :title="name"
      :loading="store.current === null"
      :crumbs="crumbs(name)"
    />
    <IntegrationConfigPanel :slug="slug" />
  </UiStack>
</template>
