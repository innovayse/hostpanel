<template>
  <!--
    Deliberately `info`, not `error`. Nothing failed: the API answered that this signed-in
    identity has no customer record, which is the normal state of a staff account. Rendering
    it in red would tell the person something had broken and give them nowhere to go.
  -->
  <UiAlert variant="info" :title="$t('client.noProfile.title')" data-testid="no-profile-notice">
    {{ message }}

    <template #action>
      <UiStack direction="row" align="center" gap="xs" class="flex-shrink-0">
        <!--
          The recovery action: a state the user cannot act on is a dead end. Most accounts that
          land here are new customers who have not ordered yet, so the way out is the catalogue.
          No admin panel link: this page cannot tell staff from customers, and staff know the way.
        -->
        <UiButton size="sm" variant="subtle" :to="SITE_HOSTING_PATH" data-testid="no-profile-browse">
          {{ $t('client.noProfile.browseAction') }}
        </UiButton>
      </UiStack>
    </template>
  </UiAlert>
</template>

<script setup lang="ts">
/**
 * Explains that the signed-in account has no client profile, and offers a way out.
 *
 * Shown wherever the client portal would otherwise render an empty section or a red alert
 * for an account that has no client record: a person who signed in and has not ordered yet (the
 * first order creates the record, so the action leads to the catalogue), or a staff account such
 * as the platform superadmin, who authenticates fine and has no row in the backend's `clients` table.
 *
 * The explanation is the API's own sentence, kept by `stores/client.ts` when it recognised
 * the CLIENT_PROFILE_NOT_FOUND code, and already in the caller's language: the backend
 * resolves it from `ApplicationValidationMessages*.resx` in the culture `X-Language` asked for. No
 * sentence is written in this template.
 */
import { useClientStore } from '~/stores/client'
import { SITE_HOSTING_PATH } from '~/constants/siteRoutes'

/** Translator for the strings this file builds itself. */
const { t } = useI18n()
/** Client store: the signed-in person and their services, invoices, domains and tickets. */
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
