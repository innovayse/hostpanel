<template>
  <UiCard class="space-y-4" data-testid="bundle-domain">
    <div>
      <UiHeading :level="2" size="label" class="tracking-widest">{{ $t('bundles.domain.title') }}</UiHeading>
      <UiText size="sm" tone="muted" class="mt-1" data-testid="bundle-domain-years">{{ $t('domains.years', years) }}</UiText>
    </div>

    <UiAlert v-if="error" variant="error" data-testid="bundle-domain-error">{{ error }}</UiAlert>

    <!-- The chosen domain -->
    <div v-if="selected" class="flex items-center justify-between gap-3" data-testid="bundle-domain-selected">
      <div class="min-w-0">
        <UiText weight="semibold" class="break-all">{{ selected.name }}</UiText>
        <UiText size="xs" tone="muted">{{ selected.action === DomainOrderAction.Transfer ? $t('domains.tabTransfer') : $t('domains.tabRegister') }}</UiText>
      </div>
      <UiButton variant="subtle" size="sm" data-testid="bundle-domain-change" @click="emit('clear')">
        {{ $t('bundles.domain.change') }}
      </UiButton>
    </div>

    <template v-else>
      <UiChipTabs
        v-model="action"
        :tabs="actionTabs"
        :label="$t('bundles.domain.actionLabel')"
        testid="bundle-domain-tabs"
      />

      <!-- Register: search a name across the offered extensions -->
      <div v-if="action === DomainOrderAction.Register" class="space-y-3">
        <UiForm spacing="none" @submit="emit('search', term)">
          <div class="flex flex-wrap items-end gap-2">
            <UiInput
              v-model="term"
              :placeholder="$t('domains.placeholder')"
              :disabled="!hasZones"
              class="min-w-[200px] flex-1"
              testid="bundle-domain-input"
            />
            <UiButton type="submit" :disabled="!hasZones" data-testid="bundle-domain-search">{{ $t('domains.search') }}</UiButton>
          </div>
        </UiForm>

        <UiText v-if="!hasZones" size="sm" tone="muted" data-testid="bundle-domain-no-zones">{{ $t('aurora.domainSearch.noZones') }}</UiText>
        <UiSkeleton v-else-if="pending" :lines="3" data-testid="bundle-domain-pending" />
        <div v-else class="space-y-2">
          <UiCard
            v-for="result in results"
            :key="result.name"
            padding="sm" class="flex flex-wrap items-center justify-between gap-3 shadow-none"
            :data-testid="`bundle-domain-result-${result.name}`"
          >
            <div class="flex min-w-0 items-center gap-3">
              <UiText class="break-all font-mono">{{ result.name }}</UiText>
              <UiBadge size="sm" rounded :variant="AVAILABILITY_TONES[result.status]">
                {{ $t(`aurora.domainSearch.${result.status}`) }}
              </UiBadge>
            </div>
            <div class="flex items-center gap-3">
              <UiText weight="bold">{{ result.price }}</UiText>
              <UiButton
                v-if="result.status === DomainResultStatus.Available"
                size="sm"
                :data-testid="`bundle-domain-pick-${result.name}`"
                @click="emit('select', { action: DomainOrderAction.Register, name: result.name, eppCode: '' })"
              >
                {{ $t('bundles.domain.pick') }}
              </UiButton>
              <UiButton
                v-else-if="result.status === DomainResultStatus.Taken"
                variant="subtle"
                size="sm"
                :data-testid="`bundle-domain-transfer-${result.name}`"
                @click="startTransfer(result.name)"
              >
                {{ $t('aurora.domainSearch.transfer') }}
              </UiButton>
            </div>
          </UiCard>
        </div>
      </div>

      <!-- Transfer: the name and its authorization code -->
      <UiForm v-else spacing="sm" @submit="submitTransfer">
        <UiInput
          v-model="transferName"
          :label="$t('domains.transferDomainLabel')"
          :placeholder="$t('domains.transferDomainPlaceholder')"
          testid="bundle-domain-transfer-name"
        />
        <UiInput
          v-model="transferEpp"
          :label="$t('domains.transferAuthLabel')"
          :placeholder="$t('domains.transferEppPlaceholder')"
          testid="bundle-domain-transfer-epp"
        />
        <UiButton type="submit" full-width data-testid="bundle-domain-transfer-submit">{{ $t('bundles.domain.useTransfer') }}</UiButton>
      </UiForm>
    </template>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * The domain step of a bundle that includes one: register a name found by search, or transfer one
 * in with its authorization code. The registration period is fixed by the bundle and shown, never
 * chosen here.
 *
 * Presentational: the page owns the domain search and the pricing. This component reports the
 * typed term (`search`), the chosen domain (`select`) and the wish to pick another (`clear`).
 */
import { computed, ref } from 'vue'
import { AVAILABILITY_TONES } from '~/utils/domainStatus'
import type { DomainResult } from '~/types/domainresult'
import { DomainResultStatus } from '~/types/domainresultstatus'
import type { BundleDomainChoice } from '~/types/bundledomainchoice'
import type { ChipTab } from '~/types/chiptab'
import { DomainOrderAction } from '~/types/domainorderaction'

/** Props of BundleDomainPicker. */
interface Props {
  /** Years of registration the bundle includes; fixed, shown to the visitor. */
  years: number
  /** The availability rows of the last search. */
  results: DomainResult[]
  /** Whether a search is under way. */
  pending: boolean
  /** False when the operator has priced no extensions. */
  hasZones: boolean
  /** The domain the visitor chose; null while none is chosen. */
  selected: BundleDomainChoice | null
  /** Why the last choice was refused; empty when it was accepted. */
  error: string
}

defineProps<Props>()

/** Events of BundleDomainPicker. */
const emit = defineEmits<{
  /** The visitor searched for a name. */
  search: [term: string]
  /** The visitor chose a domain. */
  select: [choice: BundleDomainChoice]
  /** The visitor wants to pick another domain. */
  clear: []
}>()

/** Translator for the active locale. */
const { t } = useI18n()

/** Whether the domain is registered or transferred in. */
const action = ref<DomainOrderAction>(DomainOrderAction.Register)

/** The two ways to get the domain. */
const actionTabs = computed<ChipTab<DomainOrderAction>[]>(() => [
  { value: DomainOrderAction.Register, label: t('domains.tabRegister'), testid: 'bundle-domain-tab-register' },
  { value: DomainOrderAction.Transfer, label: t('domains.tabTransfer'), testid: 'bundle-domain-tab-transfer' },
])

/** The name typed in the search field. */
const term = ref('')

/** The name typed in the transfer form. */
const transferName = ref('')

/** The authorization code typed in the transfer form. */
const transferEpp = ref('')

/**
 * Moves a taken name to the transfer form.
 *
 * @param name The taken domain the visitor wants to transfer in.
 */
const startTransfer = (name: string): void => {
  transferName.value = name
  action.value = DomainOrderAction.Transfer
}

/** Reports the typed transfer as the chosen domain. */
const submitTransfer = (): void => {
  emit('select', { action: DomainOrderAction.Transfer, name: transferName.value.trim().toLowerCase(), eppCode: transferEpp.value.trim() })
}
</script>
