import { test } from 'node:test'
import assert from 'node:assert/strict'
import { normalizeReleases, formatReleaseDate, loadReleases } from '../.vitepress/lib/releases.js'

const IDS = ['UkrUtils.Katottg', 'UkrUtils.Katottg.Import', 'UkrUtils.Katottg.Tool']
const rel = (o = {}) => ({
  tag_name: 'v1.0.1', name: 'N', draft: false, prerelease: false,
  published_at: '2026-01-01T00:00:00Z', html_url: 'https://github.com/x', body: 'b', ...o,
})
const REPO = 'UkrUtils/Katottg'

test('drops drafts', () => {
  assert.equal(normalizeReleases([rel({ draft: true }), rel()], IDS).length, 1)
})
test('sorts by published_at desc then tag desc', () => {
  const r = normalizeReleases([
    rel({ tag_name: 'v1.0.0', published_at: '2026-01-01T00:00:00Z' }),
    rel({ tag_name: 'v1.1.0', published_at: '2026-02-01T00:00:00Z' }),
    rel({ tag_name: 'v1.0.1', published_at: '2026-02-01T00:00:00Z' }),
  ], IDS)
  assert.deepEqual(r.map(x => x.tag), ['v1.1.0', 'v1.0.1', 'v1.0.0'])
})
test('version strips leading v', () => {
  const r = normalizeReleases([rel({ tag_name: 'v1.0.1' }), rel({ tag_name: '1.2.0' })], IDS)
  assert.deepEqual(r.map(x => x.version).sort(), ['1.0.1', '1.2.0'])
})
test('name falls back to tag', () => {
  assert.equal(normalizeReleases([rel({ name: null })], IDS)[0].name, 'v1.0.1')
  assert.equal(normalizeReleases([rel({ name: '' })], IDS)[0].name, 'v1.0.1')
})
test('null body becomes empty string', () => {
  assert.equal(normalizeReleases([rel({ body: null })], IDS)[0].bodyMarkdown, '')
})
test('prerelease flag passes through', () => {
  assert.equal(normalizeReleases([rel({ prerelease: true })], IDS)[0].prerelease, true)
})
test('builds nuget links', () => {
  const p = normalizeReleases([rel()], IDS)[0].packages
  assert.deepEqual(p[0], { id: 'UkrUtils.Katottg', url: 'https://www.nuget.org/packages/UkrUtils.Katottg/1.0.1' })
  assert.equal(p.length, 3)
})
test('url is html_url', () => {
  assert.equal(normalizeReleases([rel({ html_url: 'https://g/h' })], IDS)[0].url, 'https://g/h')
})
test('empty input gives empty list', () => {
  assert.deepEqual(normalizeReleases([], IDS), [])
})
test('formats date in UTC', () => {
  assert.equal(formatReleaseDate('2026-11-05T23:30:00Z', 'uk'), '05.11.2026')
  assert.equal(formatReleaseDate('2026-11-05T23:30:00Z', 'en'), 'Nov 5, 2026')
})

function fake(status, body = [], calls = []) {
  const f = async (url, init) => {
    calls.push({ url, init })
    if (status === 'throw') throw new Error('net down')
    return { status, ok: status === 200, json: async () => body }
  }
  return f
}

test('loadReleases sends token and headers', async () => {
  const calls = []
  await loadReleases({ fetchImpl: fake(200, [], calls), env: { GITHUB_TOKEN: 't' }, warn() {}, repo: REPO, packageIds: IDS })
  assert.equal(calls[0].url, 'https://api.github.com/repos/UkrUtils/Katottg/releases?per_page=100')
  assert.equal(calls[0].init.headers.Authorization, 'Bearer t')
  assert.equal(calls[0].init.headers['User-Agent'], 'UkrUtils.Katottg-site')
  assert.equal(calls[0].init.headers.Accept, 'application/vnd.github+json')
  const calls2 = []
  await loadReleases({ fetchImpl: fake(200, [], calls2), env: {}, warn() {}, repo: REPO, packageIds: IDS })
  assert.equal('Authorization' in calls2[0].init.headers, false)
})
test('loadReleases throws on CI failure', async () => {
  await assert.rejects(loadReleases({ fetchImpl: fake(500), env: { CI: 'true' }, warn() {}, repo: REPO, packageIds: IDS }), /500/)
})
test('loadReleases warns locally on failure', async () => {
  for (const s of [403, 'throw']) {
    const warns = []
    const r = await loadReleases({ fetchImpl: fake(s), env: {}, warn: m => warns.push(m), repo: REPO, packageIds: IDS })
    assert.deepEqual(r, [])
    assert.equal(warns.length, 1)
  }
})
test('loadReleases warns at 100 releases', async () => {
  const warns = []
  const body = Array.from({ length: 100 }, (_, i) => rel({ tag_name: `v1.0.${i}` }))
  const r = await loadReleases({ fetchImpl: fake(200, body), env: {}, warn: m => warns.push(m), repo: REPO, packageIds: IDS })
  assert.equal(r.length, 100)
  assert.ok(warns.some(w => /pagination/.test(w)))
})
