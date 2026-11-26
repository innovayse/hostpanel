<script setup lang="ts">
/**
 * Layout wrapper for client detail pages.
 * Shows a glass header card (avatar, name, status, key figures, export), the section tabs under it and the
 * active sub-page below. While the client is read, the header keeps its shape with bars in place of the avatar,
 * the name, the status and the figures, and the tabs are already there; the sub-page waits for the client.
 *
 * It also draws the trail of a section page (Clients, the client, the section) above the card, so every client page has
 * its way up to the client list, a phone included. A detail page (a domain, a ticket) draws its own longer trail in its
 * header, and the layout then leaves its own out so no step shows twice.
 */
import { computed, onBeforeUnmount, onMounted, watch, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { usePageCrumbs } from '@/composables/usePageCrumbs'
import { CLIENT_NAV } from '@/lib/clientSections'
import { clientPath } from '@/lib/routes'
import { useClientsStore } from '@/stores/clients'
import { usePageTitleStore } from '@/stores/pageTitle'
import { CLIENT_STATUS_OPTIONS } from '@/lib/constants'
import { clientStatusTone, statusLabel } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { formatPlainAmount } from '@/lib/money'
import ClientExportModal from '@/components/clients/ClientExportModal.vue'
import ClientGroupBadge from '@/components/clients/ClientGroupBadge.vue'
import ClientSectionTabs from '@/components/clients/ClientSectionTabs.vue'
import RoutedView from '@/components/layout/RoutedView.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiBreadcrumb from '@/components/ui/UiBreadcrumb.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiBreadcrumbVariant } from '@/utils/ui/breadcrumb'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiStatRowVariant } from '@/utils/ui/statRow'
import type { UiStatFigure } from '@/utils/ui/statRow'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import schema, { ClientStatKey } from '@/layouts/ClientLayout.schema'

const { t } = useI18n()

/** Whether the export dialog is open. */
const showExportModal = ref(false)

/** Current route, source of the client id. */
const route = useRoute()

/** Clients store, owner of the loaded client. */
const store = useClientsStore()

/** Builds the trail of a section page. */
const { sectionCrumbs } = usePageCrumbs()

/** Tells the top bar that this layout draws the trail, and whether a page header already does. */
const pageTitle = usePageTitleStore()

/** Identifies this layout to the page title store. */
const owner = Symbol('client-layout')

/** The section of the client the route is on (its summary, its domains list, a domain of it); absent for an address that is no section. */
const section = computed(() => {
  const rest = route.path.slice(`${clientPath(String(route.params.id))}/`.length)
  return CLIENT_NAV.find((entry) => entry.section === rest.split('/')[0])?.section
})

/** The trail drawn above the card; empty when the address is no section, the client could not be read, or the page's own header draws a longer one. */
const trail = computed<UiBreadcrumbItem[]>(() => {
  if (section.value === undefined || (store.error && !store.current) || pageTitle.trailOwnedElsewhere(owner)) {
    return []
  }
  return sectionCrumbs(String(route.params.id), section.value)
})

watch(() => trail.value.length > 0, (draws) => pageTitle.announce(owner, undefined, draws), { immediate: true })

onBeforeUnmount(() => pageTitle.release(owner))

/** The key figures of the open client by the key the schema gives them; empty while the client is being read (the row then draws bars). */
const figures = computed((): Record<string, UiStatFigure> => {
  const client = store.current
  if (!client) {
    return {}
  }
  return {
    [ClientStatKey.Id]: { value: String(client.id) },
    [ClientStatKey.Email]: { value: client.email },
    [ClientStatKey.Joined]: { value: formatDate(client.createdAt) },
    [ClientStatKey.TwoFactor]: { value: client.twoFactorEnabled ? t('clients.clientsList.on') : t('clients.clientsList.off') },
    [ClientStatKey.Credit]: { value: formatPlainAmount(client.creditBalance ?? 0, client.preferences.currency) },
  }
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
  <UiStack :gap="UiGap.Md" class="w-full">
    <UiAlert v-if="store.error && !store.current" :variant="UiTone.Danger" data-testid="client-layout-error">
      {{ store.error }}
    </UiAlert>

    <template v-else>
      <UiBreadcrumb v-if="trail.length > 0" :items="trail" :variant="UiBreadcrumbVariant.Page" current-testid="client-layout-crumb-current" />

      <!--
        The header card. The way up to the client list is the breadcrumb trail of the page below (a detail page draws it in its header) and the top bar. The first
        row holds the avatar, the name block (name over status, group and company)
        and export; on a phone the name block is one line (name, status, then group and company wrapping after) and
        export drops to a row of its own at the end. The key
        figures sit under it in a grid that is two columns wide on a phone.
      -->
      <UiCard :padding="UiCardPadding.Md" :aria-busy="store.current ? undefined : 'true'" :data-testid="store.current ? 'client-layout-header' : 'client-layout-loading'">
        <UiStack :gap="UiGap.Md">
          <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Md" reflow-below-md>
            <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Md" class="min-w-0 flex-1">
              <UiBadge v-if="store.current" :tone="UiTone.Primary" data-testid="client-layout-avatar">
                {{ store.current.firstName.charAt(0) }}{{ store.current.lastName.charAt(0) }}
              </UiBadge>
              <UiSkeletonBar v-else class="h-6 w-10 shrink-0 rounded-full" aria-hidden="true" />
              <UiStack v-if="!store.current" :gap="UiGap.Xs" class="min-w-0 flex-1" aria-hidden="true">
                <UiSkeletonBar class="h-7 w-56 max-w-full rounded-md" />
                <UiSkeletonBar class="h-4 w-40 rounded-md" />
              </UiStack>
              <UiStack v-else :gap="UiGap.Xs" reflow-below-md class="min-w-0 flex-1">
                <UiHeading :level="1" :variant="UiHeadingVariant.Section" class="min-w-0 break-words" data-testid="client-header-name">
                  {{ store.current.firstName }} {{ store.current.lastName }}
                </UiHeading>
                <!-- On a phone the name block is one wrapping line, so this row steps aside (`contents`) and the status joins the name's line while the group and company wrap after it -->
                <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap class="max-md:contents">
                  <UiBadge :tone="clientStatusTone(store.current.status)" small dot>{{ statusLabel(CLIENT_STATUS_OPTIONS, store.current.status) }}</UiBadge>
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
            <UiButton :icon="Icon.Download"
              :variant="UiButtonVariant.Secondary"
              :size="UiSize.Sm"
              class="self-end md:self-auto"
              :disabled="!store.current"
              data-testid="client-layout-export"
              @click="showExportModal = true"
            >{{ t('shared.export') }}
            </UiButton>
          </UiStack>
          <UiStatRow
            v-if="schema.stats"
            :stats="schema.stats"
            :figures="figures"
            :variant="UiStatRowVariant.Inline"
            testid-prefix="client-layout-stat"
            data-testid="client-layout-stats"
          />
        </UiStack>
      </UiCard>

      <ClientExportModal
        v-if="showExportModal && store.current"
        :client-id="Number(route.params.id)"
        :client-name="`${store.current.firstName} ${store.current.lastName}`"
        @close="showExportModal = false"
      />

      <ClientSectionTabs :client-id="String(route.params.id)" />
      <UiStack v-if="store.current" :gap="UiGap.None" class="min-w-0 flex-1">
        <RoutedView />
      </UiStack>
    </template>
  </UiStack>
</template>
