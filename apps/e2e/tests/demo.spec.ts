import { expect, test, type Browser, type Page } from "@playwright/test";
import { apps } from "../playwright.config";

/**
 * The Phase 1 demo script (spec §43), as the guest and the staff see it. Alex, in room 412, chats with the Aurora Grand
 * in the guest app; housekeeping works in the staff app. No manager takes part.
 */

/** Signs in as a dev persona (local builds offer them in place of real sign-in). */
async function signInAs(page: Page, url: string, persona: string) {
  await page.goto(url);
  await page.getByRole("button", { name: persona }).click();
}

/** Sends the guest's message and returns the concierge's reply to it. */
async function ask(page: Page, question: string) {
  const replies = page.getByRole("list", { name: "Messages" }).locator('li[data-sender="Concierge"]');
  const before = await replies.count();

  await page.getByRole("textbox", { name: "Message" }).fill(question);
  await page.getByRole("button", { name: "Send" }).click();
  await expect(replies).toHaveCount(before + 1);

  return replies.nth(before);
}

/** The staff app as whichever housekeeper the work was routed to: the system decides, not a manager. */
async function housekeeperWith(browser: Browser, card: RegExp): Promise<Page> {
  for (const persona of ["Jo Okafor", "Sam Patel"]) {
    const page = await (await browser.newContext()).newPage();
    await signInAs(page, apps.staff, persona);

    try {
      await expect(page.getByRole("article", { name: card })).toBeVisible({ timeout: 10_000 });

      return page;
    } catch {
      await page.context().close();
    }
  }

  throw new Error("No housekeeper was given the work.");
}

test("Demos 1 to 4: the guest's questions are answered from the hotel's own records", async ({ page }) => {
  await signInAs(page, apps.guest, "Alex Morgan");

  // Demo 1: canonical operating hours.
  await expect(await ask(page, "What time does the pool close tonight?")).toContainText("The pool is open daily from 07:00 to 22:00.");
  // Demo 2: canonical policy.
  await expect(await ask(page, "Can my 12-year-old use the gym?")).toContainText("Children aged 12 to 15 may use the gym with an adult");
  // Demo 3: canonical menus, by dietary tag.
  await expect(await ask(page, "What vegetarian food can I get here?")).toContainText("paneer tikka (GBP 12.50)");
  // Demo 4: the property's events tonight.
  await expect(await ask(page, "What's happening tonight?")).toContainText("Jazz night is on in the lobby bar");
});

test("Demo 5: two towels go to room 412 and the guest is told, with no manager involved", async ({ page, browser }) => {
  await signInAs(page, apps.guest, "Alex Morgan");

  // The system interprets the request, creates the service request and its work, and routes it.
  await expect(await ask(page, "Send two towels to my room.")).toHaveText(/I've asked our team to bring 2 bath towels to Room 412\./);
  const requests = page.getByRole("region", { name: "Your requests" });
  await expect(requests).toContainText("Room 412");
  await expect(requests).toContainText("Received");

  // Staff sees "Room 412 · Deliver 2 bath towels" and completes it.
  const housekeeper = await housekeeperWith(browser, /^Room 412: /);
  const card = housekeeper.getByRole("article", { name: /^Room 412: / });
  await expect(card).toContainText("2 × bath towel");
  await card.getByRole("button", { name: "Accept" }).click();
  await card.getByRole("button", { name: "Start" }).click();
  // Complete asks for an optional note, then completes.
  await card.getByRole("button", { name: "Complete" }).click();
  await card.getByRole("button", { name: "Complete" }).click();
  await expect(housekeeper.getByRole("article", { name: /^Room 412: / })).toHaveCount(0);

  // The system consumes the completion, completes the request, and tells the guest.
  const conversation = page.getByRole("list", { name: "Messages" });
  await expect(conversation.locator('li[data-sender="Concierge"]').last()).toContainText("Good news: your 2 bath towels have been delivered to Room 412.");
  await expect(requests).toContainText("Nothing pending.");
  await housekeeper.context().close();
});
