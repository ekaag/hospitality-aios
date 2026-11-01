import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { WorkCard, type StaffMember, type WorkItem } from "./WorkCard";

const now = new Date("2026-10-31T10:30:00Z");

function towels(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: "01a17cb1-0000-7000-8000-000000000001",
    propertyId: "aa000000-0000-4000-8000-000000000003",
    actionType: "DeliverItems",
    status: "Assigned",
    instructions: "Deliver 2 × Bath towel",
    location: "Room 412",
    reason: "Guest asked for towels",
    priority: "Normal",
    items: [{ name: "Bath towel", quantity: 2, code: "bath_towel" }],
    skillsRequired: ["housekeeping"],
    requestedAt: "2026-10-31T10:22:00Z",
    deadline: "2026-10-31T10:42:00Z",
    assignedTo: "aa000000-0000-4000-8000-000000000301",
    assigneeName: "Jo Okafor",
    endedAt: null,
    completionNote: null,
    evidence: [],
    failureReason: null,
    actions: ["Accept"],
    ...overrides,
  };
}

const buttons = () => screen.queryAllByRole("button").map((button) => button.textContent);

afterEach(cleanup);

describe("WorkCard", () => {
  it("shows the card of spec §32 with only the steps the API allows", () => {
    render(<WorkCard item={towels()} onStep={vi.fn()} now={now} />);

    expect(screen.getByRole("heading", { name: "Room 412" })).toBeTruthy();
    expect(screen.getByText("2 × bath towel")).toBeTruthy();
    expect(screen.getByText("Normal")).toBeTruthy();
    expect(screen.getByText("8 minutes ago")).toBeTruthy();
    expect(buttons()).toEqual(["ACCEPT"]);
  });

  it.each([
    [["Start", "Fail"], ["START", "FAIL"]],
    [["Complete", "Fail"], ["COMPLETE", "FAIL"]],
    [["Claim"], ["CLAIM"]],
    [["RequestAgain"], ["REQUEST AGAIN"]],
    [[], []],
  ] as const)("offers %j as %j", (actions, expected) => {
    render(<WorkCard item={towels({ actions: [...actions] })} onStep={vi.fn()} now={now} />);

    expect(buttons()).toEqual(expected);
  });

  it("fails only with a reason", async () => {
    const onStep = vi.fn(() => Promise.resolve());
    render(<WorkCard item={towels({ status: "InProgress", actions: ["Complete", "Fail"] })} onStep={onStep} now={now} />);

    fireEvent.click(screen.getByRole("button", { name: "FAIL" }));
    const submit = screen.getAllByRole("button", { name: "FAIL" })[0] as HTMLButtonElement;
    expect(submit.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText("Why can't it be done?"), { target: { value: "Could not access the room" } });
    fireEvent.click(submit);

    await waitFor(() => expect(onStep).toHaveBeenCalledWith(expect.objectContaining({ location: "Room 412" }), { kind: "fail", reason: "Could not access the room" }));
  });

  it("completes with the note, if one is given", async () => {
    const onStep = vi.fn(() => Promise.resolve());
    render(<WorkCard item={towels({ status: "InProgress", actions: ["Complete", "Fail"] })} onStep={onStep} now={now} />);

    fireEvent.click(screen.getByRole("button", { name: "COMPLETE" }));
    fireEvent.change(screen.getByLabelText("Note (optional)"), { target: { value: " Left at the door " } });
    fireEvent.click(screen.getByRole("button", { name: "COMPLETE" }));

    await waitFor(() => expect(onStep).toHaveBeenCalledWith(expect.anything(), { kind: "complete", note: "Left at the door" }));
  });

  it("offers a manager only staff on duty with the skills the work needs", () => {
    const roster: StaffMember[] = [
      { id: "s1", propertyId: "p", displayName: "Sam Patel", skills: ["housekeeping"], isOnDuty: true, version: 1 },
      { id: "s2", propertyId: "p", displayName: "Jo Okafor", skills: ["housekeeping"], isOnDuty: false, version: 1 },
      { id: "s3", propertyId: "p", displayName: "Ravi Mehta", skills: ["maintenance"], isOnDuty: true, version: 1 },
    ];
    render(<WorkCard item={towels({ status: "Requested", assignedTo: null, assigneeName: null, actions: ["Assign"] })} roster={roster} onStep={vi.fn()} now={now} />);

    fireEvent.click(screen.getByRole("button", { name: "ASSIGN" }));

    expect(screen.getAllByRole("option").map((option) => option.textContent)).toEqual(["Choose someone on duty", "Sam Patel"]);
  });
});
