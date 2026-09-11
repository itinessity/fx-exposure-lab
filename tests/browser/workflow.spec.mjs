import { test, expect } from '@playwright/test';
test.beforeEach(async ({page})=>{
 await page.route('https://fonts.googleapis.com/**',r=>r.fulfill({contentType:'text/css',body:''}));
 await page.route('https://tile.openstreetmap.org/**',r=>r.fulfill({contentType:'image/svg+xml',body:'<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256"><rect width="256" height="256" fill="#1d3440"/></svg>'}));
});
test('complete the main workflow',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('/');

await page.locator('#date').fill('2026-09-01');await page.locator('#calculate').click();await expect(page.locator('#rows tr')).toHaveCount(4);
await expect(page.locator('#export')).toBeEnabled();await expect(page.locator('#baseline')).not.toHaveText('—');
await page.locator('#date').fill('2026-08-31');await page.locator('#calculate').click();await expect(page.locator('#history button')).toHaveCount(2);
await page.locator('#history button').last().click();await expect(page.locator('#status')).toContainText('No rates were refreshed');

 expect(errors).toEqual([]);
});
test('mobile layout stays within the viewport',async({page})=>{
 await page.setViewportSize({width:390,height:844});await page.goto('/');
 await expect(page.locator('h1')).toBeVisible();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBeTruthy();
});
