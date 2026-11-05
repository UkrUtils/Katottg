import { test } from 'node:test'
import assert from 'node:assert/strict'
import { donateUrl } from '../.vitepress/lib/donate.js'

test('порожній ідентифікатор дає null', () => {
  assert.equal(donateUrl(''), null)
  assert.equal(donateUrl('  '), null)
})
test('будує посилання PayPal Donate', () => {
  assert.equal(donateUrl('ABC123'), 'https://www.paypal.com/donate/?hosted_button_id=ABC123')
})
test('кодує значення через encodeURIComponent', () => {
  assert.equal(donateUrl('a b&c'), 'https://www.paypal.com/donate/?hosted_button_id=a%20b%26c')
})
test('обрізає пробіли навколо ідентифікатора', () => {
  assert.equal(donateUrl(' ABC '), 'https://www.paypal.com/donate/?hosted_button_id=ABC')
})
