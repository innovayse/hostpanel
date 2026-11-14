<script setup lang="ts">
/**
 * One CWP7 server on the CWP7 integration page: its name, state dot, hostname, address and account usage,
 * as a link to the servers page.
 *
 * While `server` is null the card draws the same layout with bars where the values go, so the grid keeps its
 * shape until the servers have arrived; it is then no link, as there is nowhere to go yet.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'
import type { ServerDto } from '@/types/server'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for Cwp7ServerCard. */
const props = withDefaults(defineProps<{
  /** The server to show; null while the servers are still loading. */
  server?: ServerDto | null
}>(), {
  server: null,
})

/** Tone of the server's last known state: success when online, danger when offline, neutral when never tested. */
const stateTone = computed<UiTone>(() => {
  if (props.server?.isOnline === true) {
    return UiTone.Success
  }
  return props.server?.isOnline === false ? UiTone.Danger : UiTone.Neutral
})
</script>

<template>
  <UiLink :to="server ? AppRoute.Servers : undefined" class="block" :data-testid="server ? `cwp7-server-${server.id}` : 'cwp7-server-loading'">
    <UiCard padding="md" class="h-full" :aria-busy="server ? undefined : 'true'">
      <UiStack gap="sm">
        <UiStack v-if="server" direction="row" align="center" gap="sm">
          <UiHeading :level="3" variant="card" class="min-w-0 flex-1 truncate">{{ server.name }}</UiHeading>
          <UiBadge v-if="server.limits.isDisabled" :tone="UiTone.Danger" small>{{ t('shared.disabled') }}</UiBadge>
          <UiBadge v-else :tone="stateTone" dot small />
        </UiStack>
        <UiSkeletonBar v-else class="h-5 w-40 rounded-md" aria-hidden="true" />
        <UiText v-if="server" tone="muted" size="sm">{{ server.endpoint.hostname }}</UiText>
        <UiSkeletonBar v-else class="h-3.5 w-32 rounded-md" aria-hidden="true" />
        <template v-if="server">
          <UiText v-if="server.endpoint.ipAddress" tone="muted" size="sm">{{ server.endpoint.ipAddress }}</UiText>
          <UiText v-if="server.accountsCount !== null" tone="muted" size="sm">
            {{ t('integrations.cwp7Integration.accountscountValueAccounts', { accountsCount: server.accountsCount, value: server.limits.maxAccounts ?? '∞' }) }}
          </UiText>
        </template>
        <template v-else>
          <UiSkeletonBar class="h-3.5 w-24 rounded-md" aria-hidden="true" />
          <UiSkeletonBar class="h-3.5 w-28 rounded-md" aria-hidden="true" />
        </template>
      </UiStack>
    </UiCard>
  </UiLink>
</template>
