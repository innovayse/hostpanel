<script setup lang="ts">
/**
 * Layout wrapper for client detail pages.
 * Shows a glass header card (avatar, name, status, key figures, export), the section tabs under it and the
 * active sub-page below. While the client is read, the header keeps its shape with bars in place of the avatar,
 * the name, the status and the figures, and the tabs are already there; the sub-page waits for the client.
 */
import { computed, onMounted, watch, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientsStore } from '@/stores/clients'
import { clientStatusTone } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { formatPlainAmount } from '@/lib/money'
import ClientExportModal from '@/components/clients/ClientExportModal.vue'
import ClientGroupBadge from '@/components/clients/ClientGroupBadge.vue'
import ClientSectionTabs from '@/components/clients/ClientSectionTabs.vue'
import RoutedView from '@/components/layout/RoutedView.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** Whether the export dialog is open. */
const showExportModal = ref(false)

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to leave for the clients list. */
const router = useRouter()

/** Clients store, owner of the loaded client. */
const store = useClientsStore()

/** One figure of the header card: a caption over its value. */
interface HeaderStat {
  /** Key of the figure; also the suffix of its `data-testid`. */
  key: string
  /** Translated caption. */
  label: string
  /** The value as it is read; null while the client is being read. */
  value: string | null
}

/** The key figures of the open client, in the order the card shows them. */
const stats = computed<HeaderStat[]>(() => {
  const client = store.current
  return [
    { key: 'id', label: t('shared.id'), value: client ? String(client.id) : null },
    { key: 'email', label: t('shared.email'), value: client ? client.email : null },
    { key: 'joined', label: t('clients.clientsList.joined'), value: client ? formatDate(client.createdAt) : null },
    {
      key: 'two-factor',
      label: t('clients.clientsList.2fa'),
      value: client ? (client.twoFactorEnabled ? t('clients.clientsList.on') : t('clients.clientsList.off')) : null,
    },
    {
      key: 'credit',
      label: t('clients.clientCredit.credit'),
      value: client ? formatPlainAmount(client.creditBalance ?? 0, client.preferences.currency) : null,
    },
  ]
})

/**
 * Loads the client data and currencies for the current route param.
 *
 * @param id - The client identifier from route params.
 * @returns Promise that resolves when both fetches complete.
 */
const loadClient = async (id: string): Promise<void> => {
  await store.fetchById(id)
  store.fetchCurrencies()
}

/**
 * Leaves the client for the clients list.
 *
 * @returns Nothing.
 */
const goBack = (): void => {
  router.push(AppRoute.Clients)
}

// Re-fetch when client ID changes in the route
watch(() => route.params.id, (newId) => {
  if (newId) {
    loadClient(String(newId))
  }
})

onMounted(() => {
  loadClient(String(route.params.id))
})
</script>

<template>
  <UiStack gap="md" class="w-full">
    <UiAlert v-if="store.error && !store.current" variant="error" data-testid="client-layout-error">
      {{ store.error }}
    </UiAlert>

    <template v-else>
      <!--
        The header card. The first row holds back, the avatar, the name block (name over status, group and company)
        and export; on a phone the name block is one line (name, status, then group and company wrapping after) and
        export drops to a row of its own at the end. The key
        figures sit under it in a grid that is two columns wide on a phone.
      -->
      <UiCard padding="md" :aria-busy="store.current ? undefined : 'true'" :data-testid="store.current ? 'client-layout-header' : 'client-layout-loading'">
        <UiStack gap="md">
          <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" gap="md" reflow-below-md>
            <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" gap="md" class="min-w-0 flex-1">
              <UiIconButton :label="t('shared.back')" data-testid="client-layout-back" @click="goBack">
                <UiIcon :markup="Icon.Back" />
              </UiIconButton>
              <UiBadge v-if="store.current" :tone="UiTone.Primary" data-testid="client-layout-avatar">
                {{ store.current.firstName.charAt(0) }}{{ store.current.lastName.charAt(0) }}
              </UiBadge>
              <UiSkeletonBar v-else class="h-6 w-10 shrink-0 rounded-full" aria-hidden="true" />
              <UiStack v-if="!store.current" gap="xs" class="min-w-0 flex-1" aria-hidden="true">
                <UiSkeletonBar class="h-7 w-56 max-w-full rounded-md" />
                <UiSkeletonBar class="h-4 w-40 rounded-md" />
              </UiStack>
              <UiStack v-else gap="xs" reflow-below-md class="min-w-0 flex-1">
                <UiHeading :level="1" variant="section" class="min-w-0 break-words" data-testid="client-header-name">
                  {{ store.current.firstName }} {{ store.current.lastName }}
                </UiHeading>
                <!-- On a phone the name block is one wrapping line, so this row steps aside (`contents`) and the status joins the name's line while the group and company wrap after it -->
                <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" gap="sm" wrap class="max-md:contents">
                  <UiBadge :tone="clientStatusTone(store.current.status)" small dot>{{ store.current.status }}</UiBadge>
                  <ClientGroupBadge
                    v-if="store.current.clientGroupName && store.current.clientGroupColor"
                    :name="store.current.clientGroupName"
                    :color="store.current.clientGroupColor"
                  />
                  <UiText v-if="store.current.companyName" :tone="UiTextTone.Secondary" :size="UiTextSize.Sm">
                    {{ store.current.companyName }}
                  </UiText>
                </UiStack>
              </UiStack>
            </UiStack>
            <UiButton
              :variant="UiButtonVariant.Secondary"
              :size="UiSize.Sm"
              class="self-end md:self-auto"
              :disabled="!store.current"
              data-testid="client-layout-export"
              @click="showExportModal = true"
            >
              <UiIcon :markup="Icon.Download" :size="UiSize.Sm" />
              {{ t('clients.clientLayout.exportData') }}
            </UiButton>
          </UiStack>
          <UiGrid :columns="6" gap="sm" data-testid="client-layout-stats">
            <UiStack
              v-for="stat in stats"
              :key="stat.key"
              gap="xs"
              class="min-w-0"
              :data-testid="`client-layout-stat-${stat.key}`"
            >
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">
                {{ stat.label }}
              </UiText>
              <UiSkeletonBar v-if="stat.value === null" class="h-4 w-20 rounded-md" aria-hidden="true" />
              <UiText v-else :tone="UiTextTone.Primary" :size="UiTextSize.Sm" class="truncate font-medium">
                {{ stat.value }}
              </UiText>
            </UiStack>
          </UiGrid>
        </UiStack>
      </UiCard>

      <ClientExportModal
        v-if="showExportModal && store.current"
        :client-id="Number(route.params.id)"
        :client-name="`${store.current.firstName} ${store.current.lastName}`"
        @close="showExportModal = false"
      />

      <ClientSectionTabs :client-id="String(route.params.id)" />
      <UiStack v-if="store.current" gap="none" class="min-w-0 flex-1">
        <RoutedView />
      </UiStack>
    </template>
  </UiStack>
</template>
