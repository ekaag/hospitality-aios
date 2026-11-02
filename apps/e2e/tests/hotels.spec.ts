import { expect, test } from "@playwright/test";
import { apps } from "../playwright.config";

/**
 * The reference tenant runs two hotels, the Aurora Grand and the Mysore Grand: the duty manager switches between them
 * in the staff app, and each shows its own guests and its own look.
 */
test("the duty manager switches from the Aurora Grand to the Mysore Grand, and the choice is remembered", async ({ page }) => {
  await page.goto(apps.staff);
  await page.getByRole("button", { name: "Dana Reyes" }).click();

  const hotel = page.getByRole("combobox", { name: "Hotel" });
  await expect(hotel).toHaveValue(/.+/);
  await hotel.selectOption({ label: "Aurora Grand" });
  await page.getByRole("button", { name: "Guests" }).click();
  await expect(page.getByRole("rowheader", { name: "Alex Morgan" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-hotel", "aurora-grand");

  await hotel.selectOption({ label: "Mysore Grand" });
  await page.getByRole("button", { name: "Guests" }).click();
  await expect(page.getByText("No guests are reserved or in house.")).toBeVisible();
  await expect(page.locator(".hero-title")).toHaveText("Mysore Grand");
  await expect(page.locator("html")).toHaveAttribute("data-hotel", "mysore-grand");

  // The choice is remembered on this device.
  await page.reload();
  await expect(page.getByRole("combobox", { name: "Hotel" })).toHaveValue(await hotel.inputValue());
  await expect(page.locator(".hero-title")).toHaveText("Mysore Grand");
});
