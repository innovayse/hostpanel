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
 * The wording is resolved through `utils/portalErrorMessages.ts`, the single mapping table
 * for backend error codes; no sentence is written in this template.
 */
import { PortalErrorCode, portalErrorMessageKey } from '~/utils/portalErrorMessages'

const { t } = useI18n()

/**
 * The explanation, looked up by code in the one mapping table.
 *
 * Falls back to the code itself only if the table and the constant ever drift apart, which
 * would be a bug rather than a state worth wording.
 */
const message = computed(() => {
  const key = portalErrorMessageKey(PortalErrorCode.ClientProfileNotFound)
  return key ? t(key) : PortalErrorCode.ClientProfileNotFound
})
</script>
