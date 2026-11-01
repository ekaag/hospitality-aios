import type { ApiClient, Schema } from "@hospitalityos/api-client";
import { useEffect, useState, type FormEvent } from "react";

type StaffAccess = Schema<"StaffAccessResponse">;
type StaffInvite = Schema<"StaffInviteResponse">;
type StaffRole = NonNullable<Schema<"StaffRole">>;

const statuses: Record<Schema<"StaffAccessStatus">, string> = {
  Invited: "Invited",
  Active: "Active",
  Revoked: "Revoked",
};

const problems: Record<string, string> = {
  "access.staff_member_taken": "That staff member already has access; send them a new code instead.",
  "access.revoke_self": "You cannot revoke your own access.",
  "access.revoked": "That access has been revoked; invite them again instead.",
};

/**
 * Who may sign in (K4b), for administrators: invite someone with a one-time code, shown once to pass on; send a new
 * code (a lost account, or one that expired); and revoke access, which takes effect on their next request. Workers are
 * invited as a roster entry, so the work routed to them is theirs.
 */
export function Access({ client, propertyId }: { client: ApiClient; propertyId: string }) {
  const [entries, setEntries] = useState<StaffAccess[] | undefined>();
  const [roster, setRoster] = useState<Schema<"StaffMemberResponse">[]>([]);
  const [invite, setInvite] = useState<StaffInvite | undefined>();
  const [problem, setProblem] = useState<string | undefined>();
  const [name, setName] = useState("");
  const [role, setRole] = useState<StaffRole>("Worker");
  const [staffMemberId, setStaffMemberId] = useState("");

  // Bumped after each change, to read the list again.
  const [changes, setChanges] = useState(0);

  useEffect(() => {
    let current = true;

    void Promise.all([client.GET("/api/v1/access"), client.GET("/api/v1/staff", { params: { query: { propertyId } } })]).then(([listed, staff]) => {
      if (current) {
        setEntries(listed.data);
        setRoster(staff.data ?? []);
        setProblem(listed.data ? undefined : "Access could not be read.");
      }
    });

    return () => {
      current = false;
    };
  }, [client, propertyId, changes]);

  const reload = () => setChanges((count) => count + 1);

  function failed(error: unknown, fallback: string) {
    const code = (error as { code?: string } | undefined)?.code;
    setProblem((code && problems[code]) ?? fallback);
  }

  async function grant(event: FormEvent) {
    event.preventDefault();
    const { data, error } = await client.POST("/api/v1/access", {
      body: { displayName: name, role, ...(role === "Worker" && staffMemberId ? { staffMemberId } : {}) },
    });

    if (!data) {
      failed(error, "Could not invite them.");
      return;
    }

    setInvite(data);
    setName("");
    setStaffMemberId("");
    reload();
  }

  async function reinvite(entry: StaffAccess) {
    const { data, error } = await client.POST("/api/v1/access/{id}/invite", { params: { path: { id: entry.id } } });

    if (!data) {
      failed(error, `Could not send ${entry.displayName} a new code.`);
      return;
    }

    setInvite(data);
    reload();
  }

  async function revoke(entry: StaffAccess) {
    const { data, error } = await client.POST("/api/v1/access/{id}/revoke", { params: { path: { id: entry.id } } });

    if (!data) {
      failed(error, `Could not revoke ${entry.displayName}'s access.`);
      return;
    }

    setInvite(undefined);
    reload();
  }

  const unlinked = roster.filter((member) => !entries?.some((entry) => entry.staffMemberId === member.id && entry.status !== "Revoked"));

  return (
    <section aria-labelledby="section" className="access">
      <h2 id="section">People &amp; access</h2>
      <p>Invite someone with a one-time code. They sign in, enter the code once, and from then on act with the role you give them.</p>
      {problem && <p role="alert">{problem}</p>}
      {invite && (
        <aside aria-label="New invite">
          <p>
            Give {invite.access.displayName} this code. It is shown only now and works once, until{" "}
            {new Date(invite.expiresAt).toLocaleString()}:
          </p>
          <p>
            <code>{invite.code}</code>
          </p>
        </aside>
      )}
      <form onSubmit={(event) => void grant(event)}>
        <label>
          Name <input value={name} onChange={(event) => setName(event.target.value)} required />
        </label>{" "}
        <label>
          Role{" "}
          <select value={role} onChange={(event) => setRole(event.target.value as StaffRole)}>
            <option value="Worker">Worker</option>
            <option value="Manager">Manager</option>
            <option value="Admin">Admin</option>
          </select>
        </label>{" "}
        {role === "Worker" && (
          <label>
            Roster entry{" "}
            <select value={staffMemberId} onChange={(event) => setStaffMemberId(event.target.value)} required>
              <option value="">Choose…</option>
              {unlinked.map((member) => (
                <option key={member.id} value={member.id}>
                  {member.displayName}
                </option>
              ))}
            </select>
          </label>
        )}{" "}
        <button type="submit">Invite</button>
      </form>
      {entries && (
        <table>
          <caption>Access</caption>
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th scope="col">Role</th>
              <th scope="col">Status</th>
              <th scope="col">
                <span className="visually-hidden">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.id}>
                <th scope="row">{entry.displayName}</th>
                <td>{entry.role}</td>
                <td>{statuses[entry.status]}</td>
                <td>
                  {entry.status !== "Revoked" && (
                    <>
                      <button type="button" onClick={() => void reinvite(entry)}>
                        New code
                      </button>{" "}
                      <button type="button" onClick={() => void revoke(entry)}>
                        Revoke
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
