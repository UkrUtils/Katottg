/**
 * Будує посилання на PayPal Donate за ідентифікатором розміщеної кнопки.
 * @param {string} buttonId ідентифікатор кнопки (hosted_button_id)
 * @returns {string | null} посилання або null, якщо ідентифікатор порожній
 */
export function donateUrl(buttonId) {
  const id = (buttonId ?? '').trim()
  if (!id) return null
  return `https://www.paypal.com/donate/?hosted_button_id=${encodeURIComponent(id)}`
}
