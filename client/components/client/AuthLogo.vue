<template>
  <NuxtLink to="/" class="inline-block mb-6">
    <!--
      Plain img for an uploaded logo, NuxtImg for the bundled one: IPX optimises by
      reading the file off local disk, and an upload is proxied from the API's
      wwwroot/uploads rather than living in this app's public/ — the same split
      components/layout/Header.vue makes.
    -->
    <img
      v-if="logoOnDark"
      :src="logoOnDark"
      :alt="alt"
      loading="eager"
      class="h-12 w-auto max-w-[200px] mx-auto object-contain"
    />
    <NuxtImg
      v-else
      src="/logo.svg"
      :alt="alt"
      width="160"
      height="48"
      loading="eager"
      class="h-12 w-auto mx-auto"
    />
  </NuxtLink>
</template>

<script setup lang="ts">
/**
 * Logo above the sign-in, register, forgot-password, reset-password and
 * accept-invite forms.
 *
 * Those pages paint a fixed dark background whatever the site's colour mode, so this
 * always picks the dark-background logo rather than the one for the page mode. One
 * component rather than five copies of the same markup, which is what the pages had.
 */
const { logoOnDark, alt } = useBrandLogo()
</script>
