import { createMarkdownRenderer } from 'vitepress'
import { REPO, PACKAGE_IDS } from '../site.config.js'
import { loadReleases } from '../lib/releases.js'

// Завантажувач даних на етапі збірки: релізи GitHub + HTML нотаток.
export default {
  async load() {
    const releases = await loadReleases({
      fetchImpl: fetch,
      env: process.env,
      warn: msg => console.warn(`[releases] ${msg}`),
      repo: REPO,
      packageIds: PACKAGE_IDS,
    })
    const config = globalThis.VITEPRESS_CONFIG
    const md = await createMarkdownRenderer(config.srcDir, config.markdown, config.site.base, config.logger)
    return releases.map(r => ({ ...r, bodyHtml: md.render(r.bodyMarkdown) }))
  },
}
