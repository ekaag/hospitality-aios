import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Chat, type Message } from "./Chat";

const said = (id: string, sender: Message["sender"], text: string): Message => ({
  id,
  conversationId: "c1",
  sender,
  text,
  clientMessageId: null,
  sentAt: "2026-10-31T10:22:00Z",
});

afterEach(cleanup);

describe("Chat", () => {
  it("names who wrote each message, never which department", () => {
    render(
      <Chat
        messages={[said("1", "Guest", "Two towels please"), said("2", "Concierge", "Of course."), said("3", "System", "Your towels have been delivered.")]}
        pending={[]}
        waiting={false}
        onSend={vi.fn()}
        onRetry={vi.fn()}
      />,
    );

    const from = screen.getAllByRole("listitem").map((item) => item.querySelector(".from")?.textContent);
    expect(from).toEqual(["You", "Concierge", "Update"]);
  });

  it("offers what a guest can ask only while the conversation is empty", () => {
    const { rerender } = render(<Chat messages={[]} pending={[]} waiting={false} onSend={vi.fn()} onRetry={vi.fn()} />);
    expect(screen.getByRole("button", { name: "What time does the pool close?" })).toBeTruthy();

    rerender(<Chat messages={[said("1", "Guest", "Hi")]} pending={[]} waiting={false} onSend={vi.fn()} onRetry={vi.fn()} />);
    expect(screen.queryByRole("button", { name: "What time does the pool close?" })).toBeNull();
  });

  it("sends trimmed text with Enter, and nothing when empty", async () => {
    const onSend = vi.fn(() => Promise.resolve());
    render(<Chat messages={[said("1", "Guest", "Hi")]} pending={[]} waiting={false} onSend={onSend} onRetry={vi.fn()} />);
    const box = screen.getByLabelText("Message");

    expect((screen.getByRole("button", { name: "Send" }) as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(box, { target: { value: "  Is the gym open?  " } });
    fireEvent.keyDown(box, { key: "Enter" });

    await waitFor(() => expect(onSend).toHaveBeenCalledWith("Is the gym open?"));
    expect((box as HTMLTextAreaElement).value).toBe("");
  });

  it("shows that a reply is on its way", () => {
    render(<Chat messages={[said("1", "Guest", "Hi")]} pending={[]} waiting onSend={vi.fn()} onRetry={vi.fn()} />);

    expect(screen.getByRole("status").textContent).toBe("The concierge is replying…");
  });
});
