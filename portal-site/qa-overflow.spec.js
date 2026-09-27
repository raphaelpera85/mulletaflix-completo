const { test, expect } = require('playwright/test');

const routes = ['/', '/downloads', '/docs', '/privacy', '/updates', '/community'];

for (const width of [1440, 1024, 390]) {
  for (const route of routes) {
    test(`${route} has no horizontal overflow at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await page.goto(`https://mulletaflix-portal.vercel.app${route}?qa=860c79e`, { waitUntil: 'networkidle' });
      const metrics = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
        overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth
      }));
      expect(metrics.overflow, `${route} at ${width}px: ${JSON.stringify(metrics)}`).toBe(0);
    });
  }
}
