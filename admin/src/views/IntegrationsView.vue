<script setup lang="ts">
/**
 * Integration category page.
 *
 * Shows all integrations for the active category derived from the route param.
 * Falls back to 'payments' when accessed at /integrations.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { useIntegrationsStore } from '@/stores/integrations'
import { INTEGRATION_CATEGORIES } from '@/types/integrationmeta'
import { IntegrationCategory } from '@/types/integration'
import IntegrationCard from '@/components/integrations/IntegrationCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'

/** Translation function. */
const { t } = useI18n()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/** Current route, whose param names the category. */
const route = useRoute()

/** Integrations store: the list and the refusal sentence. */
const store = useIntegrationsStore()

/** Active category key from route param, defaults to 'payments'. */
const activeCategory = computed<string>(() =>
  (route.params.category as string) || IntegrationCategory.Payments,
)

/** Label for the active category. */
const activeCategoryLabel = computed<string>(() =>
  INTEGRATION_CATEGORIES.find(c => c.key === activeCategory.value)?.label ?? activeCategory.value,
)

/** Placeholder cards drawn in the grid while the list loads; the real count is only known once it has arrived. */
const LOADING_CARD_COUNT = 6

/** Integrations filtered to the active category. */
const filtered = computed(() =>
  store.integrations.filter(i => i.category === activeCategory.value),
)

/** True until the first read of the list has answered: the page draws its real layout with placeholders where the values go. */
const loading = computed<boolean>(() => store.loading && filtered.value.length === 0)

/** Number of active integrations in this category. */
const activeCount = computed<number>(() => filtered.value.filter(i => i.isEnabled).length)

/** Header line: how many integrations, and how many are active. */
const subtitle = computed<string>(() => {
  const total = t('integrations.integrations.lengthIntegrationValue', { length: filtered.value.length })
  return activeCount.value > 0
    ? `${total} ${t('integrations.integrations.activecountActive', { activeCount: activeCount.value })}`
    : total
})

/** Loads the integrations. */
onMounted(store.fetchAll)
</script>

<template>
  <UiStack gap="md" class="w-full">
    <UiPageHeader :title="activeCategoryLabel" :subtitle="loading ? undefined : subtitle">
      <UiSkeletonBar v-if="loading" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" aria-hidden="true" />
      <template v-if="guideUrl" #actions>
        <PageHelpLink :url="guideUrl" />
      </template>
    </UiPageHeader>

    <!-- Error -->
    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="integrations-error"
      @retry="store.fetchAll()"
    />

    <!-- Cards grid; while the list loads the same grid holds placeholder cards -->
    <UiGrid v-else-if="loading || filtered.length > 0" :columns="3" gap="md" data-testid="integrations-grid">
      <template v-if="loading">
        <IntegrationCard v-for="index in LOADING_CARD_COUNT" :key="index" />
      </template>
      <template v-else>
        <IntegrationCard
          v-for="integration in filtered"
          :key="integration.slug"
          :integration="integration"
        />
      </template>
    </UiGrid>

    <!-- Empty state -->
    <UiEmptyState
      v-else
      :title="t('integrations.integrations.noIntegrationsInThisCategory')"
      data-testid="integrations-empty"
    />
  </UiStack>
</template>
