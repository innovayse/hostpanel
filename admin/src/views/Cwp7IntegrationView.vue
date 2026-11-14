<script setup lang="ts">
/**
 * CWP7 module landing page.
 *
 * Shows module info and quick-action buttons to create servers or products
 * linked to the CWP7 provisioning module.
 * Lists existing CWP7 servers with their status.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import type { RouteLocationRaw } from 'vue-router'
import { useServersStore } from '@/stores/servers'
import { INTEGRATION_META } from '@/types/integrationmeta'
import { IntegrationSlug } from '@/types/integration'
import { ServerModule } from '@/types/server'
import type { ServerDto } from '@/types/server'
import Cwp7ServerCard from '@/components/integrations/Cwp7ServerCard.vue'
import IntegrationBreadcrumb from '@/components/integrations/IntegrationBreadcrumb.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTextSize } from '@/utils/ui/text'

/** Translation function. */
const { t } = useI18n()

/** Servers store: the server list. */
const serversStore = useServersStore()

/** Static metadata of the CWP7 integration (logo and tile colour). */
const meta = INTEGRATION_META[IntegrationSlug.Cwp7]

/** Placeholder cards drawn in the servers grid while the list loads. */
const LOADING_CARD_COUNT = 3

/** CWP7 servers only. */
const cwp7Servers = computed<ServerDto[]>(() =>
  serversStore.servers.filter(s => s.module === ServerModule.Cwp7),
)

/** Route of the servers page with the CWP7 server form open, opened by the create server links. */
const createServerRoute: RouteLocationRaw = { path: AppRoute.Servers, query: { create: ServerModule.Cwp7 } }

/** Route of the products page with a CWP7 product form open, opened by the create product link. */
const createProductRoute: RouteLocationRaw = { path: AppRoute.SettingsProducts, query: { create: 'true', module: ServerModule.Cwp7 } }

/** Loads the servers. */
onMounted(() => {
  void serversStore.fetchServers()
})
</script>

<template>
  <UiStack gap="lg" class="w-full">
    <IntegrationBreadcrumb :name="t('integrations.cwp7Integration.cwp7')" />

    <!-- Module header card -->
    <UiCard padding="md" data-testid="cwp7-header">
      <UiStack direction="row" align="start" gap="md">
        <UiStack align="center" justify="center" class="h-14 w-14 shrink-0 rounded-xl p-2" :class="meta.color">
          <UiImage :src="meta.logo" alt="" class="h-full w-full object-contain brightness-0 invert" />
        </UiStack>
        <UiStack gap="sm" class="min-w-0 flex-1">
          <UiHeading :level="1" variant="section">{{ t('integrations.cwp7Integration.cwp7') }}</UiHeading>
          <UiText tone="muted" size="md">{{ t('integrations.cwp7Integration.controlWebPanel7Provisioning') }}</UiText>

          <UiGrid :columns="3" gap="md">
            <UiStack gap="xs">
              <UiText tone="muted" size="sm">{{ t('shared.category') }}</UiText>
              <UiText tone="primary" size="sm" class="font-medium">{{ t('integrations.cwp7Integration.hostingProvisioning') }}</UiText>
            </UiStack>
            <UiStack gap="xs">
              <UiText tone="muted" size="sm">{{ t('integrations.cwp7Integration.moduleType') }}</UiText>
              <UiText tone="primary" size="sm" class="font-medium">{{ t('integrations.cwp7Integration.serverModule') }}</UiText>
            </UiStack>
            <UiStack gap="xs">
              <UiText tone="muted" size="sm">{{ t('integrations.cwp7Integration.developer') }}</UiText>
              <UiText tone="primary" size="sm" class="font-medium">{{ t('shared.productName') }}</UiText>
            </UiStack>
          </UiGrid>
        </UiStack>
      </UiStack>
    </UiCard>

    <!-- Action buttons -->
    <UiGrid :columns="2" gap="md">
      <UiCard padding="md">
        <UiStack gap="sm" align="start">
          <UiHeading :level="3" variant="card">{{ t('integrations.cwp7Integration.createNewServer') }}</UiHeading>
          <UiText tone="muted" size="sm">{{ t('integrations.cwp7Integration.addACwp7ServerWith') }}</UiText>
          <UiLink :to="createServerRoute" :variant="UiButtonVariant.Secondary" data-testid="cwp7-create-server">
            {{ t('integrations.cwp7Integration.createNewServer') }}
          </UiLink>
        </UiStack>
      </UiCard>
      <UiCard padding="md">
        <UiStack gap="sm" align="start">
          <UiHeading :level="3" variant="card">{{ t('integrations.cwp7Integration.createNewProduct') }}</UiHeading>
          <UiText tone="muted" size="sm">{{ t('integrations.cwp7Integration.createAHostingPlanLinked') }}</UiText>
          <UiLink :to="createProductRoute" :variant="UiButtonVariant.Secondary" data-testid="cwp7-create-product">
            {{ t('integrations.cwp7Integration.createNewProduct') }}
          </UiLink>
        </UiStack>
      </UiCard>
    </UiGrid>

    <!-- CWP7 Servers list -->
    <UiStack gap="sm">
      <UiStack direction="row" align="center" justify="between">
        <UiHeading :level="2" variant="section">{{ t('integrations.cwp7Integration.cwp7Servers') }}</UiHeading>
        <UiResultCount :loading="serversStore.loading" :size="UiTextSize.Sm">{{ t('integrations.cwp7Integration.lengthServerValue', { length: cwp7Servers.length }) }}</UiResultCount>
      </UiStack>

      <!-- Empty state -->
      <UiCard v-if="!serversStore.loading && cwp7Servers.length === 0" padding="md" data-testid="cwp7-servers-empty">
        <UiStack gap="sm" align="center">
          <UiText tone="muted" size="md">{{ t('integrations.cwp7Integration.noCwp7ServersConfiguredYet') }}</UiText>
          <UiLink :to="createServerRoute" :variant="UiButtonVariant.Link" data-testid="cwp7-add-first-server">
            {{ t('integrations.cwp7Integration.addYourFirstServer') }}
          </UiLink>
        </UiStack>
      </UiCard>

      <!-- Server cards; while the servers load the same grid holds placeholder cards -->
      <UiGrid v-else :columns="3" gap="md" data-testid="cwp7-servers">
        <template v-if="serversStore.loading">
          <Cwp7ServerCard v-for="index in LOADING_CARD_COUNT" :key="index" />
        </template>
        <template v-else>
          <Cwp7ServerCard v-for="server in cwp7Servers" :key="server.id" :server="server" />
        </template>
      </UiGrid>
    </UiStack>
  </UiStack>
</template>
