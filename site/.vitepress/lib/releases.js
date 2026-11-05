// Завантаження та нормалізація історії релізів з GitHub API.

/** @typedef {{ tag: string, version: string, name: string, publishedAt: string, prerelease: boolean, url: string, bodyMarkdown: string, packages: { id: string, url: string }[] }} Release */

/**
 * Перетворює відповідь GitHub API на список релізів: без чернеток, найновіші спершу.
 * @param {object[]} raw
 * @param {string[]} packageIds
 * @returns {Release[]}
 */
export function normalizeReleases(raw, packageIds) {
  return raw
    .filter(r => !r.draft)
    .map(r => {
      const tag = r.tag_name
      const version = tag.replace(/^v/, '')
      return {
        tag,
        version,
        name: r.name || tag,
        publishedAt: r.published_at,
        prerelease: Boolean(r.prerelease),
        url: r.html_url,
        bodyMarkdown: r.body ?? '',
        packages: packageIds.map(id => ({
          id,
          url: `https://www.nuget.org/packages/${id}/${version}`,
        })),
      }
    })
    .sort((a, b) => {
      if (a.publishedAt !== b.publishedAt) return a.publishedAt < b.publishedAt ? 1 : -1
      return a.tag < b.tag ? 1 : a.tag > b.tag ? -1 : 0
    })
}

/**
 * Форматує дату релізу в UTC.
 * @param {string} iso
 * @param {'uk' | 'en'} lang
 */
export function formatReleaseDate(iso, lang) {
  const date = new Date(iso)
  if (lang === 'uk') {
    return new Intl.DateTimeFormat('uk-UA', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' }).format(date)
  }
  return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' }).format(date)
}

/**
 * Завантажує релізи. У CI помилка кидається, локально — попередження і порожній список.
 * @returns {Promise<Release[]>}
 */
export async function loadReleases({ fetchImpl, env, warn, repo, packageIds }) {
  const url = `https://api.github.com/repos/${repo}/releases?per_page=100`
  const headers = {
    Accept: 'application/vnd.github+json',
    'X-GitHub-Api-Version': '2022-11-28',
    'User-Agent': 'katottg-site-build',
  }
  if (env.GITHUB_TOKEN) headers.Authorization = `Bearer ${env.GITHUB_TOKEN}`

  let raw
  try {
    const res = await fetchImpl(url, { headers })
    if (res.status !== 200) throw new Error(`GitHub API returned status ${res.status} for ${url}`)
    raw = await res.json()
  } catch (e) {
    if (env.CI) throw e
    warn(`Не вдалося завантажити релізи (${e.message}); сторінка буде порожньою.`)
    return []
  }

  if (raw.length >= 100) {
    warn('Отримано 100 релізів: потрібна pagination, решту не завантажено.')
  }
  return normalizeReleases(raw, packageIds)
}
