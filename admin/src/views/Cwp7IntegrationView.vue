<script setup lang="ts">
/**
 * CWP7 module landing page.
 *
 * Shows module info and quick-action buttons to create servers or products
 * linked to the CWP7 provisioning module.
 * Lists existing CWP7 servers with their status. The page is the cards of `Cwp7IntegrationView.schema` in `UiDetailCards`;
 * while the servers load the servers card draws its placeholder boxes and the other cards, which are static, show themselves.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import type { RouteLocationRaw } from 'vue-router'
import { useServersStore } from '@/stores/servers'
import { INTEGRATION_META } from '@/types/integrationmeta'
import { IntegrationSlug } from '@/types/integration'
import { ServerModule } from '@/types/server'
import type { ServerOptionDto } from '@/types/server'
import Cwp7ServerCard from '@/components/integrations/Cwp7ServerCard.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
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
import schema from '@/views/Cwp7IntegrationView.schema'
import { UiActionKind } from '@/utils/ui/actions'

/** Translation function. */
const { t } = useI18n()

/** Servers store: the lean server lookup. */
const serversStore = useServersStore()

/** Static metadata of the CWP7 integration (logo and tile colour). */
const meta = INTEGRATION_META[IntegrationSlug.Cwp7]

/** Key of the servers card in the schema, the one card whose data loads on its own. */
const SERVERS_CARD_KEY = 'servers'

/** CWP7 servers only: the lookup is asked for that module, so nothing is filtered in the browser. */
const cwp7Servers = computed<ServerOptionDto[]>(() => serversStore.serverOptions)

/** Route of the servers page with the CWP7 server form open, opened by the create server links. */
const createServerRoute: RouteLocationRaw = { path: AppRoute.Servers, query: { create: ServerModule.Cwp7 } }

/** Route of the products page with a CWP7 product form open, opened by the create product link. */
const createProductRoute: RouteLocationRaw = { path: AppRoute.SettingsProducts, query: { create: 'true', module: ServerModule.Cwp7 } }

/** Loads the servers. */
onMounted(() => {
  void serversStore.fetchServerOptions(ServerModule.Cwp7)
})
</script>

<template>
  <UiDetailCards :columns="schema.columns" :ratio="schema.ratio" :loading-keys="serversStore.optionsLoading ? [SERVERS_CARD_KEY] : []" class="w-full" data-testid="cwp7-cards">
    <!-- Module header card -->
    <template #card-module>
      <UiStack direction="row" align="start" gap="md" data-testid="cwp7-header">
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
    </template>

    <!-- Action cards -->
    <template #card-createServer>
      <UiStack gap="sm" align="start">
        <UiText tone="muted" size="sm">{{ t('integrations.cwp7Integration.addACwp7ServerWith') }}</UiText>
        <UiLink :action="UiActionKind.Add" :to="createServerRoute" :variant="UiButtonVariant.Secondary" data-testid="cwp7-create-server">
          {{ t('integrations.cwp7Integration.createNewServer') }}
        </UiLink>
      </UiStack>
    </template>
    <template #card-createProduct>
      <UiStack gap="sm" align="start">
        <UiText tone="muted" size="sm">{{ t('integrations.cwp7Integration.createAHostingPlanLinked') }}</UiText>
        <UiLink :action="UiActionKind.Add" :to="createProductRoute" :variant="UiButtonVariant.Secondary" data-testid="cwp7-create-product">
          {{ t('integrations.cwp7Integration.createNewProduct') }}
        </UiLink>
      </UiStack>
    </template>

    <!-- CWP7 servers list -->
    <template #card-servers>
      <UiStack gap="sm">
        <UiResultCount :size="UiTextSize.Sm">{{ t('integrations.cwp7Integration.lengthServerValue', { length: cwp7Servers.length }) }}</UiResultCount>

        <!-- Empty state -->
        <UiStack v-if="cwp7Servers.length === 0" gap="sm" align="center" data-testid="cwp7-servers-empty">
          <UiText tone="muted" size="md">{{ t('integrations.cwp7Integration.noCwp7ServersConfiguredYet') }}</UiText>
          <UiLink :to="createServerRoute" :variant="UiButtonVariant.Link" data-testid="cwp7-add-first-server">
            {{ t('integrations.cwp7Integration.addYourFirstServer') }}
          </UiLink>
        </UiStack>

        <UiGrid v-else :columns="3" gap="md" data-testid="cwp7-servers">
          <Cwp7ServerCard v-for="server in cwp7Servers" :key="server.id" :server="server" />
        </UiGrid>
      </UiStack>
    </template>
  </UiDetailCards>
</template>
