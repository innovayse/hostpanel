<template>
  <!--
    Deliberately `info`, not `error`. Nothing failed: the API answered that this signed-in
    identity has no customer record, which is the normal state of a staff account. Rendering
    it in red told the person something had broken and gave them nowhere to go — the defect
    this component replaces.
  -->
  <UiAlert variant="info" :title="$t('client.noProfile.title')">
    {{ message }}

    <template #action>
      <div class="flex flex-shrink-0 items-center gap-2">
        <!--
          The recovery action: a state the user cannot act on is a dead end. The admin panel
          is a separate app on the same host, so it is an href rather than a route — a
          NuxtLink would try to resolve it inside this app and 404.
        -->
        <UiButton size="sm" variant="subtle" href="/admin">
          {{ $t('client.noProfile.adminAction') }}
        </UiButton>
        <UiButton size="sm" variant="subtle" to="/hosting">
          {{ $t('client.noProfile.browseAction') }}
        </UiButton>
      </div>
    </template>
  </UiAlert>
</template>

<script setup lang="ts">
/**
 * Explains that the signed-in account has no client profile, and offers a way out.
 *
 * Shown wherever the client portal would otherwise render an empty section or a red alert
 * for an account that simply is not a customer — most often the platform superadmin, who
 * authenticates fine and has no row in the backend's `clients` table.
 *
 * The explanation is the API's own sentence, kept by `stores/client.ts` when it recognised
 * the CLIENT_PROFILE_NOT_FOUND code, and already in the caller's language — the backend
 * resolves it from `ValidationMessages*.resx` in the culture `Accept-Language` asked for. No
 * sentence is written in this template, and there is no longer a mapping table to look one up
 * in.
 */
import { useClientStore } from '~/stores/client'

const { t } = useI18n()
const store = useClientStore()

/**
 * The explanation the API gave.
 *
 * The local string is the offline fallback and nothing else: this component only renders once
 * `clientProfileMissing` is set, which only happens from a refusal that carried a body, so in
 * practice the store's message is always there. A request that never reached the API leaves it
 * null, and then this says the same thing in the page's own language.
 */
const message = computed(() => store.clientProfileMessage || t('client.noProfile.body'))
</script>
