<script setup>
import { data } from './releases.data.js'
import { formatReleaseDate } from '../lib/releases.js'

const props = defineProps({ lang: { type: String, default: 'uk' } })

// Тексти інтерфейсу.
const dict = {
  uk: { empty: 'Релізів ще немає.', prerelease: 'prerelease', github: 'GitHub Release' },
  en: { empty: 'No releases yet.', prerelease: 'prerelease', github: 'GitHub Release' },
}
const t = dict[props.lang] ?? dict.uk
</script>

<template>
  <p v-if="!data.length">{{ t.empty }}</p>
  <section v-for="r in data" :key="r.tag">
    <h2 :id="r.tag">{{ r.name }}</h2>
    <p>
      <time :datetime="r.publishedAt">{{ formatReleaseDate(r.publishedAt, props.lang) }}</time>
      <Badge v-if="r.prerelease" type="warning" :text="t.prerelease" />
    </p>
    <div class="vp-doc" v-html="r.bodyHtml"></div>
    <p>
      <a :href="r.url">{{ t.github }}</a>
      <template v-for="p in r.packages" :key="p.id">
        · <a :href="p.url">{{ p.id }}</a>
      </template>
    </p>
  </section>
</template>
