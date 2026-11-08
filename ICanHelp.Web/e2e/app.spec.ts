import { expect, test } from '@playwright/test';

test('opens the announcements tab by default', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(/\/announcements$/);
  await expect(page.locator('ion-tab-bar')).toContainText('Ogłoszenia');
  await expect(page.locator('ion-tab-bar')).toContainText('Powiadomienia');
});

test('asks to sign in before opening conversations and offers registration', async ({ page }) => {
  await page.goto('/conversations');
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fconversations$/);
  await expect(page.locator('ion-title')).toContainText('Logowanie');

  await page.getByRole('link', { name: 'Załóż konto' }).click();
  await expect(page).toHaveURL(/\/register\?returnUrl=%2Fconversations$/);
});
