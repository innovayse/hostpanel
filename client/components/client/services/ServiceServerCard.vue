<template>
  <UiCard data-testid="service-server">
    <UiCardHeader :title="$t('client.services.serverInfo')" :icon="Server" />
    <UiGrid as="dl" cols="2" gap="lg">
      <UiDescriptionItem :label="$t('client.services.server')" :value="service.server?.name" />
      <UiDescriptionItem :label="$t('client.services.ipAddress')" :value="service.server?.ipAddress" value-class="font-mono text-xs" />
      <UiDescriptionItem :label="$t('client.services.username')" :value="service.username" value-class="font-mono text-xs" />
      <UiDescriptionItem v-if="hostingInfo.nameservers.length" :label="$t('client.services.nameservers')">
        <UiStack gap="none">
          <UiText v-for="nameserver in hostingInfo.nameservers" :key="nameserver" as="span" size="sm" class="font-mono">{{ nameserver }}</UiText>
        </UiStack>
      </UiDescriptionItem>
    </UiGrid>

    <UiStack
      v-if="service.usage && (hasUsageLimit(service.usage.diskLimitMb) || hasUsageLimit(service.usage.bandwidthLimitMb))"
      gap="sm"
      class="mt-6"
      data-testid="service-usage"
    >
      <ClientServicesUsageBar
        v-if="hasUsageLimit(service.usage.diskLimitMb)"
        :label="$t('client.services.disk')"
        :used-mb="service.usage.diskUsedMb"
        :limit-mb="service.usage.diskLimitMb"
      />
      <ClientServicesUsageBar
        v-if="hasUsageLimit(service.usage.bandwidthLimitMb)"
        :label="$t('client.services.bandwidth')"
        :used-mb="service.usage.bandwidthUsedMb"
        :limit-mb="service.usage.bandwidthLimitMb"
        tone="secondary"
      />
    </UiStack>

    <div v-if="hostingInfo.ssl" class="mt-6 pt-6 border-t border-gray-100 dark:border-white/10" data-testid="service-ssl">
      <UiHeading :level="3" size="subsection" class="!text-sm mb-4 flex items-center gap-2">
        <ShieldCheck v-if="hostingInfo.ssl.valid" :size="16" :stroke-width="2" class="text-green-500" />
        <ShieldX v-else :size="16" :stroke-width="2" class="text-red-400" />
        {{ $t('client.services.sslInfo') }}
      </UiHeading>
      <UiGrid as="dl" cols="2" gap="md">
        <UiDescriptionItem :label="$t('client.services.sslStatus')">
          <UiBadge :variant="hostingInfo.ssl.valid ? 'success' : 'danger'" size="sm" rounded>
            {{ hostingInfo.ssl.valid ? $t('client.services.sslValid') : $t('client.services.sslInvalid') }}
          </UiBadge>
        </UiDescriptionItem>
        <UiDescriptionItem :label="$t('client.services.sslIssuer')" :value="hostingInfo.ssl.issuer || null" />
        <UiDescriptionItem :label="$t('client.services.sslStartDate')" :value="formatDate(hostingInfo.ssl.startDate)" />
        <UiDescriptionItem :label="$t('client.services.sslExpiryDate')" :value="formatDate(hostingInfo.ssl.expiryDate)" />
      </UiGrid>
    </div>
    <div v-else-if="service.domain" class="mt-6 pt-6 border-t border-gray-100 dark:border-white/10">
      <UiText size="sm" tone="subtle" class="flex items-center gap-2" data-testid="service-ssl-missing">
        <ShieldAlert :size="16" :stroke-width="2" />
        {{ $t('client.services.sslNotDetected') }}
      </UiText>
    </div>

    <UiStack v-if="service.domain" direction="row" wrap class="mt-6 pt-6 border-t border-gray-100 dark:border-white/10">
      <UiButton :href="websiteUrl(service.domain)" target="_blank" rel="noopener noreferrer" variant="subtle" size="sm" data-testid="service-visit-website">
        <Globe :size="15" :stroke-width="2" class="mr-2" />
        {{ $t('client.services.visitWebsite') }}
      </UiButton>
      <UiButton :to="CLIENT_DOMAINS_PATH" variant="subtle" size="sm" data-testid="service-manage-domain">
        <Settings :size="15" :stroke-width="2" class="mr-2" />
        {{ $t('client.services.manageDomain') }}
      </UiButton>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ServiceServerCard: the server, IP, control-panel user and nameservers of a service, its
 * disk and bandwidth use (each shown when the server reported a limit for it), the SSL certificate (or the lack of one) and links to the website and
 * the domain page.
 */
import { Globe, Server, Settings, ShieldAlert, ShieldCheck, ShieldX } from 'lucide-vue-next'
import { CLIENT_DOMAINS_PATH } from '~/constants/clientRoutes'
import type { HostingInfo } from '~/types/hostinginfo'
import type { ClientService } from '~/types/clientservice'
import { websiteUrl } from '~/utils/controlPanel'
import { formatDate } from '~/utils/formatDate'
import { hasUsageLimit } from '~/utils/serviceUsage'

/** Props of ServiceServerCard. */
interface Props {
  /** The service being shown. */
  service: ClientService
  /** The service's nameservers and certificate. */
  hostingInfo: HostingInfo
}

defineProps<Props>()
</script>
