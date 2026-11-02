import { expect, test, type Page } from "@playwright/test";
import { apps } from "../playwright.config";

/**
 * The reference tenant runs two hotels, the Aurora Grand and the Mysore Grand. Every app has a Hotel picker, and the
 * whole app follows the hotel chosen: its people, its records, its chat, and its look.
 */

const hotelPicker = (page: Page) => page.getByRole("combobox", { name: "Hotel" });

test("the duty manager switches from the Aurora Grand to the Mysore Grand, and the choice is remembered", async ({ page }) => {
  await page.goto(apps.staff);
  await page.getByRole("button", { name: "Dana Reyes" }).click();

  await hotelPicker(page).selectOption({ label: "Aurora Grand" });
  await page.getByRole("button", { name: "Guests" }).click();
  await expect(page.getByRole("rowheader", { name: "Alex Morgan" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-hotel", "aurora-grand");

  await hotelPicker(page).selectOption({ label: "Mysore Grand" });
  await page.getByRole("button", { name: "Guests" }).click();
  await expect(page.getByRole("rowheader", { name: "Ananya Rao" })).toBeVisible();
  await expect(page.locator(".hero-title")).toHaveText("Mysore Grand");
  await expect(page.locator("html")).toHaveAttribute("data-hotel", "mysore-grand");

  // The choice is remembered on this device.
  await page.reload();
  await expect(hotelPicker(page)).toHaveValue(await hotelPicker(page).inputValue());
  await expect(page.locator(".hero-title")).toHaveText("Mysore Grand");
});

test("before signing in, the hotel picked decides who is offered: its own people", async ({ page }) => {
  await page.goto(apps.staff);

  await hotelPicker(page).selectOption({ label: "Mysore Grand" });
  await expect(page.getByRole("button", { name: "Lakshmi Iyer" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Dana Reyes" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Sam Patel" })).toHaveCount(0);

  // A worker belongs to one hotel: choosing another signs them out, to sign in as someone there.
  await page.getByRole("button", { name: "Lakshmi Iyer" }).click();
  await expect(page.locator(".hero-title")).toHaveText("Mysore Grand");
  await hotelPicker(page).selectOption({ label: "Aurora Grand" });
  await expect(page.getByRole("button", { name: "Sam Patel" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Lakshmi Iyer" })).toHaveCount(0);
});

test("the guest switches to the Mysore Grand, and its concierge answers from the Mysore Grand's records", async ({ page }) => {
  await page.goto(apps.guest);
  await hotelPicker(page).selectOption({ label: "Aurora Grand" });
  await page.getByRole("button", { name: "Alex Morgan" }).click();
  await expect(page.locator(".hero-title")).toHaveText("Welcome to the Aurora Grand");

  await hotelPicker(page).selectOption({ label: "Mysore Grand" });
  await expect(page.locator(".hero-title")).toHaveText("Welcome to the Mysore Grand");
  await expect(page.locator("html")).toHaveAttribute("data-hotel", "mysore-grand");
  await expect(page.getByText("Ananya Rao")).toBeVisible();

  const replies = page.getByRole("list", { name: "Messages" }).locator('li[data-sender="Concierge"]');
  await page.getByRole("textbox", { name: "Message" }).fill("What time does the pool close?");
  await page.getByRole("button", { name: "Send" }).click();
  await expect(replies.last()).toContainText("The courtyard pool is open daily from 06:30 to 20:00.");
});
