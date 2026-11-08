import { expect, test } from '@playwright/test';

test('opens the announcements tab by default', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(/\/announcements$/);
  await expect(page.locator('ion-tab-bar')).toContainText('Ogłoszenia');
  await expect(page.locator('ion-tab-bar')).toContainText('Powiadomienia');
});
