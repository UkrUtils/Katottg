<script setup>
import { VPButton } from 'vitepress/theme'
import { donateUrl } from '../lib/donate.js'
import { PAYPAL_BUTTON_ID } from '../site.config.js'

const props = defineProps({ lang: { type: String, default: 'uk' } })

// Тексти інтерфейсу.
const dict = {
  uk: { button: 'Підтримати через PayPal', soon: 'Кнопка донату скоро з’явиться.' },
  en: { button: 'Donate via PayPal', soon: 'The donate button is coming soon.' },
}
const t = dict[props.lang] ?? dict.uk
const url = donateUrl(PAYPAL_BUTTON_ID)
</script>

<template>
  <p v-if="url" class="donate">
    <VPButton tag="a" size="medium" theme="brand" :text="t.button" :href="url" target="_blank" rel="noopener" />
  </p>
  <p v-else>{{ t.soon }}</p>
</template>

<style scoped>
/* Стилі .vp-doc a (підкреслення, колір) не мають перебивати вигляд кнопки. */
.donate :deep(a) {
  text-decoration: none;
}
</style>
