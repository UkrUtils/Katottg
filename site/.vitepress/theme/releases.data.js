import { createMarkdownRenderer } from 'vitepress'
import { REPO, PACKAGE_IDS } from '../site.config.js'
import { loadReleases, renderReleaseNotes } from '../lib/releases.js'

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
    // bodyMarkdown на клієнт не передаємо: лише безпечний bodyHtml.
    return releases.map(({ bodyMarkdown, ...r }) => ({ ...r, bodyHtml: renderReleaseNotes(md, bodyMarkdown) }))
  },
}
