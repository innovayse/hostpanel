<script setup lang="ts">
/**
 * Dialog for creating or editing a provisioning server.
 *
 * Organized in three tabs: General, Nameservers, Server Details. Emits `saved` with the payload on
 * submit and `close` on cancel. Validation belongs to the server: a refusal comes back through the
 * `error` and `fieldErrors` props as the sentences the API returned, and the dialog switches to the
 * tab that holds the first refused field.
 */
import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { MODULE_LABELS, ServerModule } from '@/types/server'
import type { ServerDto, ServerPayload } from '@/types/server'
import type { UiTabItem } from '@/utils/ui/tabs'
import StoredSecretState from '@/components/servers/StoredSecretState.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiLimitField from '@/components/ui/UiLimitField.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'

/** Translation function. */
const { t } = useI18n()

/** Props for ServerFormModal. */
const props = withDefaults(defineProps<{
  /** Server to edit, or null when creating a new one. */
  server: ServerDto | null
  /** True while the save request is in flight. */
  saving: boolean
  /** Sentence for the last refused save, null when none. */
  error?: string | null
  /** Per-field sentences of the last refused save, keyed by camelCase field name. */
  fieldErrors?: Record<string, string>
}>(), {
  error: null,
  /**
   * Default for the field errors.
   *
   * @returns A new empty map, so instances never share one.
   */
  fieldErrors: () => ({}),
})

/** Events emitted by ServerFormModal. */
const emit = defineEmits<{
  /** Emitted when the user submits the form. */
  saved: [payload: ServerPayload]
  /** Emitted when the user closes/cancels the dialog. */
  close: []
}>()

/** Closed set of the dialog's tabs. */
const ServerFormTab = {
  /** Name, addresses, limits and flags. */
  General: 'general',
  /** The five nameserver rows. */
  Nameservers: 'nameservers',
  /** Module and credentials. */
  Details: 'details',
} as const

/** One of the dialog's tabs. */
type ServerFormTab = (typeof ServerFormTab)[keyof typeof ServerFormTab]

/** Keys of the nameserver hostname and IP fields of the form (`ns1Hostname` to `ns5Ip`). */
type NameserverKey = `ns${1 | 2 | 3 | 4 | 5}${'Hostname' | 'Ip'}`

/** The form as the inputs hold it: text fields are strings (empty = not set), the rest as in the payload. */
interface ServerFormState {
  /** Display name. */
  name: string
  /** Hostname or IP used for API access. */
  hostname: string
  /** Primary public IP address. */
  ipAddress: string
  /** Additional assigned IPs, one per line. */
  assignedIpAddresses: string
  /** Monthly cost in USD. */
  monthlyCost: number
  /** Datacenter or NOC provider name. */
  datacenter: string
  /** Account capacity limit, null for unlimited. */
  maxAccounts: number | null
  /** Status page URL. */
  serverStatusAddress: string
  /** Whether this server is disabled. */
  isDisabled: boolean
  /** Whether this is the default server for the module. */
  isDefault: boolean
  /** Control panel module type. */
  module: ServerModule
  /** API username. */
  username: string
  /** API password (empty keeps the stored one when editing). */
  password: string
  /** API token (empty keeps the stored one when editing). */
  apiToken: string
  /** Access hash (empty keeps the stored one when editing). */
  accessHash: string
  /** Remove the stored API password on save. */
  clearPassword: boolean
  /** Remove the stored API token on save. */
  clearApiToken: boolean
  /** Remove the stored access hash on save. */
  clearAccessHash: boolean
  /** Whether SSL is used for API connections. */
  useSSL: boolean
  /** Nameserver hostnames and IPs. */
  nameservers: Record<NameserverKey, string>
}

/** Module options of the select. */
const moduleOptions = (Object.keys(MODULE_LABELS) as ServerModule[]).map(k => ({ value: k, label: MODULE_LABELS[k] }))

/** Tabs of the dialog. */
const tabItems = computed<UiTabItem[]>(() => [
  { value: ServerFormTab.General, label: t('servers.serverForm.tabGeneral'), testid: 'server-form-tab-general' },
  { value: ServerFormTab.Nameservers, label: t('servers.serverForm.tabNameservers'), testid: 'server-form-tab-nameservers' },
  { value: ServerFormTab.Details, label: t('servers.serverForm.tabDetails'), testid: 'server-form-tab-details' },
])

/** The five nameserver rows: caption and the form keys of the row's two inputs. */
const nameserverRows = computed<{ index: number; label: string; hostKey: NameserverKey; ipKey: NameserverKey }[]>(() => [
  { index: 1, label: t('servers.serverForm.primary'), hostKey: 'ns1Hostname', ipKey: 'ns1Ip' },
  { index: 2, label: t('servers.serverForm.secondary'), hostKey: 'ns2Hostname', ipKey: 'ns2Ip' },
  { index: 3, label: t('servers.serverForm.third'), hostKey: 'ns3Hostname', ipKey: 'ns3Ip' },
  { index: 4, label: t('servers.serverForm.fourth'), hostKey: 'ns4Hostname', ipKey: 'ns4Ip' },
  { index: 5, label: t('servers.serverForm.fifth'), hostKey: 'ns5Hostname', ipKey: 'ns5Ip' },
])

/**
 * Builds the form for a new server, or for an existing one with its secrets blanked.
 *
 * @param s - The server being edited, or null when creating.
 * @returns The initial form state.
 */
const initialForm = (s: ServerDto | null): ServerFormState => ({
  name: s?.name ?? '',
  hostname: s?.endpoint.hostname ?? '',
  ipAddress: s?.endpoint.ipAddress ?? '',
  assignedIpAddresses: s?.endpoint.assignedIpAddresses ?? '',
  monthlyCost: s?.hosting.monthlyCost ?? 0,
  datacenter: s?.hosting.datacenter ?? '',
  maxAccounts: s?.limits.maxAccounts ?? null,
  serverStatusAddress: s?.hosting.serverStatusAddress ?? '',
  isDisabled: s?.limits.isDisabled ?? false,
  isDefault: s?.limits.isDefault ?? false,
  module: s?.module ?? ServerModule.Cwp7,
  username: s?.username ?? '',
  password: '',
  apiToken: '',
  accessHash: '',
  clearPassword: false,
  clearApiToken: false,
  clearAccessHash: false,
  useSSL: s?.endpoint.useSSL ?? true,
  nameservers: {
    ns1Hostname: s?.nameServers.ns1Hostname ?? '',
    ns1Ip: s?.nameServers.ns1Ip ?? '',
    ns2Hostname: s?.nameServers.ns2Hostname ?? '',
    ns2Ip: s?.nameServers.ns2Ip ?? '',
    ns3Hostname: s?.nameServers.ns3Hostname ?? '',
    ns3Ip: s?.nameServers.ns3Ip ?? '',
    ns4Hostname: s?.nameServers.ns4Hostname ?? '',
    ns4Ip: s?.nameServers.ns4Ip ?? '',
    ns5Hostname: s?.nameServers.ns5Hostname ?? '',
    ns5Ip: s?.nameServers.ns5Ip ?? '',
  },
})

/** The form the inputs are bound to. */
const form = reactive<ServerFormState>(initialForm(props.server))

/** Active tab. */
const tab = ref<ServerFormTab>(ServerFormTab.General)

// Loads the edited server into the form, or resets it when creating.
watch(() => props.server, (s) => {
  Object.assign(form, initialForm(s))
})

// A refused save may concern a field on a hidden tab; show the tab of the first refused field.
watch(() => props.fieldErrors, (errors) => {
  if (errors.name || errors['endpoint.hostname']) {
    tab.value = ServerFormTab.General
  } else if (errors.username) {
    tab.value = ServerFormTab.Details
  }
})

/**
 * Turns an empty text field into null, as the API expects for "not set".
 *
 * @param value - The text typed.
 * @returns The text, or null when it is empty.
 */
const orNull = (value: string): string | null => (value === '' ? null : value)

/** Builds and emits the save payload. */
const handleSubmit = (): void => {
  const ns = form.nameservers
  emit('saved', {
    name: form.name,
    endpoint: {
      hostname: form.hostname,
      ipAddress: orNull(form.ipAddress),
      assignedIpAddresses: orNull(form.assignedIpAddresses),
      useSSL: form.useSSL,
    },
    module: form.module,
    username: form.username,
    credentials: {
      password: orNull(form.password),
      apiToken: orNull(form.apiToken),
      accessHash: orNull(form.accessHash),
    },
    clears: {
      password: form.clearPassword,
      apiToken: form.clearApiToken,
      accessHash: form.clearAccessHash,
    },
    limits: {
      maxAccounts: form.maxAccounts,
      isDefault: form.isDefault,
      isDisabled: form.isDisabled,
    },
    hosting: {
      datacenter: orNull(form.datacenter),
      serverStatusAddress: orNull(form.serverStatusAddress),
      monthlyCost: form.monthlyCost,
    },
    nameServers: {
      ns1Hostname: orNull(ns.ns1Hostname),
      ns1Ip: orNull(ns.ns1Ip),
      ns2Hostname: orNull(ns.ns2Hostname),
      ns2Ip: orNull(ns.ns2Ip),
      ns3Hostname: orNull(ns.ns3Hostname),
      ns3Ip: orNull(ns.ns3Ip),
      ns4Hostname: orNull(ns.ns4Hostname),
      ns4Ip: orNull(ns.ns4Ip),
      ns5Hostname: orNull(ns.ns5Hostname),
      ns5Ip: orNull(ns.ns5Ip),
    },
  })
}
</script>

<template>
  <UiDialog
    :title="props.server ? t('servers.serverForm.editServer') : t('servers.serverForm.addNewServer')"
    size="lg"
    @close="emit('close')"
  >
    <UiStack gap="md">
      <UiAlert v-if="props.error" variant="error" data-testid="server-form-error">{{ props.error }}</UiAlert>

      <UiTabs v-model="tab" :items="tabItems" />

      <UiForm stacked @submit.prevent="handleSubmit">
        <!-- General tab -->
        <template v-if="tab === ServerFormTab.General">
          <UiGrid :columns="2" gap="sm">
            <UiField
              :label="t('shared.name')"
              :hint="t('servers.serverForm.aFriendlyLabelShownIn')"
              :error="props.fieldErrors.name"
              required
            >
              <template #default="{ id, invalid, describedby }">
                <UiInput
                  :id="id"
                  v-model="form.name"
                  type="text"
                  :invalid="invalid"
                  :aria-describedby="describedby"
                  :placeholder="t('servers.serverForm.myProductionServer')"
                  data-testid="server-form-name"
                />
              </template>
            </UiField>
            <UiField
              :label="t('servers.serverForm.hostname')"
              :hint="t('servers.serverForm.usedForApiConnectionsTo')"
              :error="props.fieldErrors['endpoint.hostname']"
              required
            >
              <template #default="{ id, invalid, describedby }">
                <UiInput
                  :id="id"
                  v-model="form.hostname"
                  type="text"
                  :invalid="invalid"
                  :aria-describedby="describedby"
                  :placeholder="t('servers.serverForm.hostnamePlaceholder')"
                  data-testid="server-form-hostname"
                />
              </template>
            </UiField>
          </UiGrid>

          <UiGrid :columns="2" gap="sm">
            <UiField :label="t('shared.ipAddress')" :error="props.fieldErrors['endpoint.ipAddress']">
              <template #default="{ id, invalid }">
                <UiInput
                  :id="id"
                  v-model="form.ipAddress"
                  type="text"
                  :invalid="invalid"
                  :placeholder="t('servers.serverForm.ipPlaceholder')"
                  data-testid="server-form-ip"
                />
              </template>
            </UiField>
            <UiField :label="t('servers.serverForm.monthlyCostUsd')" :error="props.fieldErrors['hosting.monthlyCost']">
              <UiNumberInput
                v-model="form.monthlyCost"
                :min="0"
                :step="0.01"
                :placeholder="t('servers.serverForm.costPlaceholder')"
                data-testid="server-form-cost"
              />
            </UiField>
          </UiGrid>

          <UiField
            :label="`${t('servers.serverForm.assignedIpAddresses')} ${t('servers.serverForm.onePerLine')}`"
            :error="props.fieldErrors['endpoint.assignedIpAddresses']"
          >
            <template #default="{ id, invalid }">
              <UiTextarea
                :id="id"
                v-model="form.assignedIpAddresses"
                rows="3"
                :invalid="invalid"
                :placeholder="t('servers.serverForm.assignedIpPlaceholder')"
                data-testid="server-form-assigned-ips"
              />
            </template>
          </UiField>

          <UiGrid :columns="2" gap="sm">
            <UiField :label="t('servers.serverForm.datacenterNoc')" :error="props.fieldErrors['hosting.datacenter']">
              <template #default="{ id, invalid }">
                <UiInput
                  :id="id"
                  v-model="form.datacenter"
                  type="text"
                  :invalid="invalid"
                  :placeholder="t('servers.serverForm.hetznerOvh')"
                  data-testid="server-form-datacenter"
                />
              </template>
            </UiField>
            <UiField :label="t('servers.serverForm.maxAccounts')" :error="props.fieldErrors['limits.maxAccounts']">
              <template #default="{ id, invalid }">
                <UiLimitField
                  :id="id"
                  v-model="form.maxAccounts"
                  :unlimited-value="null"
                  :min="1"
                  :invalid="invalid"
                  :placeholder="t('servers.serverForm.unlimited')"
                  data-testid="server-form-max-accounts"
                />
              </template>
            </UiField>
          </UiGrid>

          <UiField
            :label="t('servers.serverForm.serverStatusAddress')"
            :hint="t('servers.serverForm.fullPathToServerStatus')"
            :error="props.fieldErrors['hosting.serverStatusAddress']"
          >
            <template #default="{ id, invalid, describedby }">
              <UiInput
                :id="id"
                v-model="form.serverStatusAddress"
                type="text"
                :invalid="invalid"
                :aria-describedby="describedby"
                :placeholder="t('servers.serverForm.statusPlaceholder')"
                data-testid="server-form-status-address"
              />
            </template>
          </UiField>

          <UiStack direction="row" gap="lg" wrap>
            <UiLabel variant="inline">
              <UiToggle v-model="form.isDefault" data-testid="server-form-default" />
              {{ t('servers.serverForm.defaultServer') }}
            </UiLabel>
            <UiLabel variant="inline">
              <UiToggle v-model="form.isDisabled" data-testid="server-form-disabled" />
              {{ t('servers.serverForm.disableThisServer') }}
            </UiLabel>
          </UiStack>
        </template>

        <!-- Nameservers tab -->
        <template v-else-if="tab === ServerFormTab.Nameservers">
          <UiText tone="muted" size="sm">{{ t('servers.serverForm.enterNameserversThatWillBe') }}</UiText>

          <UiGrid v-for="row in nameserverRows" :key="row.index" :columns="2" gap="sm">
            <UiInput
              v-model="form.nameservers[row.hostKey]"
              type="text"
              :label="t('servers.serverForm.labelNameserver', { label: row.label })"
              :placeholder="t('servers.serverForm.nsHostPlaceholder', { n: row.index })"
              :data-testid="`server-form-ns${row.index}-host`"
            />
            <UiInput
              v-model="form.nameservers[row.ipKey]"
              type="text"
              :label="`${row.label} ${t('servers.serverForm.ip')}`"
              :placeholder="t('servers.serverForm.nsIpPlaceholder')"
              :data-testid="`server-form-ns${row.index}-ip`"
            />
          </UiGrid>
        </template>

        <!-- Server details tab -->
        <template v-else>
          <UiField :label="t('servers.serverForm.module')" :error="props.fieldErrors.module">
            <UiSelect
              v-model="form.module"
              :options="moduleOptions"
              :placeholder="t('servers.serverForm.module')"
              data-testid="server-form-module"
            />
          </UiField>

          <UiField :label="t('shared.username')" :error="props.fieldErrors.username" required>
            <template #default="{ id, invalid, describedby }">
              <UiInput
                :id="id"
                v-model="form.username"
                type="text"
                :invalid="invalid"
                :aria-describedby="describedby"
                :placeholder="t('servers.serverForm.usernamePlaceholder')"
                data-testid="server-form-username"
              />
            </template>
          </UiField>

          <UiGrid :columns="2" gap="sm">
            <UiField
              :label="t('servers.serverForm.passwordValue', { value: props.server ? t('servers.serverForm.blankKeep') : '' })"
              :error="props.fieldErrors['credentials.password']"
            >
              <template #default="{ id, invalid }">
                <UiStack gap="xs">
                  <UiInput
                    :id="id"
                    v-model="form.password"
                    type="password"
                    :invalid="invalid"
                    :placeholder="t('servers.serverForm.secretPlaceholder')"
                    data-testid="server-form-password"
                  />
                  <StoredSecretState
                    v-if="props.server"
                    :has-value="props.server.credentials.hasPassword"
                    :cleared="form.clearPassword"
                    testid="server-form-password"
                    @clear="form.clearPassword = true"
                    @cancel="form.clearPassword = false"
                  />
                </UiStack>
              </template>
            </UiField>
            <UiField
              :label="t('servers.serverForm.apiTokenValue', { value: props.server ? t('servers.serverForm.blankKeep') : '' })"
              :error="props.fieldErrors['credentials.apiToken']"
            >
              <template #default="{ id, invalid }">
                <UiStack gap="xs">
                  <UiInput
                    :id="id"
                    v-model="form.apiToken"
                    type="password"
                    :invalid="invalid"
                    :placeholder="t('servers.serverForm.secretPlaceholder')"
                    data-testid="server-form-api-token"
                  />
                  <StoredSecretState
                    v-if="props.server"
                    :has-value="props.server.credentials.hasApiToken"
                    :cleared="form.clearApiToken"
                    testid="server-form-api-token"
                    @clear="form.clearApiToken = true"
                    @cancel="form.clearApiToken = false"
                  />
                </UiStack>
              </template>
            </UiField>
          </UiGrid>

          <UiField
            :label="t('servers.serverForm.accessHashValue', { value: props.server ? t('servers.serverForm.blankKeep') : '' })"
            :hint="t('servers.serverForm.usedByCpanelWhmAnd')"
            :error="props.fieldErrors['credentials.accessHash']"
          >
            <template #default="{ id, invalid, describedby }">
              <UiStack gap="xs">
                <UiTextarea
                  :id="id"
                  v-model="form.accessHash"
                  rows="4"
                  :invalid="invalid"
                  :aria-describedby="describedby"
                  :placeholder="t('servers.serverForm.accessHashPlaceholder')"
                  class="font-mono"
                  data-testid="server-form-access-hash"
                />
                <StoredSecretState
                  v-if="props.server"
                  :has-value="props.server.credentials.hasAccessHash"
                  :cleared="form.clearAccessHash"
                  testid="server-form-access-hash"
                  @clear="form.clearAccessHash = true"
                  @cancel="form.clearAccessHash = false"
                />
              </UiStack>
            </template>
          </UiField>

          <UiLabel variant="inline">
            <UiToggle v-model="form.useSSL" data-testid="server-form-ssl" />
            {{ t('servers.serverForm.useSslForConnections') }}
          </UiLabel>
        </template>
      </UiForm>
    </UiStack>

    <template #footer>
      <UiButton type="button" variant="secondary" data-testid="server-form-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        type="button"
        variant="primary"
        :loading="props.saving"
        data-testid="server-form-save"
        @click="handleSubmit"
      >
        {{ props.server ? t('shared.saveChanges') : t('servers.serverForm.addServer') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
