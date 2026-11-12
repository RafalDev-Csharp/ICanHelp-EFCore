import { expect, test } from '@playwright/test';

test('opens the search tab by default', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(/\/announcements$/);
  const tabBar = page.locator('ion-tab-bar');
  for (const label of ['Szukaj', 'Ulubione', 'Dodaj', 'Zlecenia', 'Konto']) {
    await expect(tabBar).toContainText(label);
  }
  await expect(tabBar).not.toContainText('Powiadomienia');
});

test('asks to sign in before opening conversations and offers registration', async ({ page }) => {
  await page.goto('/conversations');
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fconversations$/);
  await expect(page.locator('ion-title')).toContainText('Logowanie');

  await page.getByRole('link', { name: 'Załóż konto' }).click();
  await expect(page).toHaveURL(/\/register\?returnUrl=%2Fconversations$/);
});
