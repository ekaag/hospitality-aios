import type { ApiClient, Schema } from "@hospitalityos/api-client";
import QRCode from "qrcode";
import { useEffect, useState } from "react";

type Stay = Schema<"StayResponse">;
type GuestLink = Schema<"GuestLinkResponse">;

const statuses: Record<Schema<"StayStatus">, string> = { Reserved: "Reserved", InHouse: "In house" };

/**
 * The front desk's check-in links (K4c): the property's reserved and in-house stays, and for each a link to give the
 * guest, shown once as an address and a QR code. A new link replaces the old one, which stops working at once, as do
 * the sessions it opened; revoking does the same without a new one.
 */
export function Guests({ client, propertyId }: { client: ApiClient; propertyId: string }) {
  const [stays, setStays] = useState<Stay[] | undefined>();
  const [shown, setShown] = useState<{ stay: Stay; link: GuestLink; qr?: string } | undefined>();
  const [problem, setProblem] = useState<string | undefined>();
  // Bumped after each change, to read the stays again.
  const [changes, setChanges] = useState(0);

  useEffect(() => {
    let current = true;

    void client.GET("/api/v1/stays", { params: { query: { propertyId } } }).then(({ data }) => {
      if (current) {
        setStays(data);
        setProblem(data ? undefined : "The stays could not be read.");
      }
    });

    return () => {
      current = false;
    };
  }, [client, propertyId, changes]);

  async function create(stay: Stay) {
    const { data } = await client.POST("/api/v1/stays/{id}/guest-link", { params: { path: { id: stay.id } } });

    if (!data) {
      setProblem(`Could not make a link for ${name(stay)}.`);
      return;
    }

    const qr = data.url ? await QRCode.toDataURL(data.url, { margin: 1, width: 220 }) : undefined;
    setShown({ stay, link: data, ...(qr ? { qr } : {}) });
    setProblem(undefined);
    setChanges((count) => count + 1);
  }

  async function revoke(stay: Stay) {
    const { data } = await client.DELETE("/api/v1/stays/{id}/guest-link", { params: { path: { id: stay.id } } });

    if (!data) {
      setProblem(`Could not revoke ${name(stay)}'s link.`);
      return;
    }

    setShown(undefined);
    setChanges((count) => count + 1);
  }

  return (
    <section aria-labelledby="guests" className="guests">
      <h2 id="guests">Guests</h2>
      {problem && <p role="alert">{problem}</p>}
      {shown && (
        <aside aria-label="Check-in link">
          <p>
            Give {name(shown.stay)} this link. It works on any of their devices until they check out, and is shown only now.
          </p>
          {shown.qr && <img src={shown.qr} alt={`QR code for ${name(shown.stay)}'s check-in link`} width={220} height={220} />}
          {shown.link.url ? (
            <p>
              <a href={shown.link.url}>{shown.link.url}</a>
            </p>
          ) : (
            <p>
              Code: <code>{shown.link.code}</code>
            </p>
          )}
        </aside>
      )}
      {stays && stays.length === 0 && <p>No guests are reserved or in house.</p>}
      {stays && stays.length > 0 && (
        <table>
          <caption>Reserved and in house</caption>
          <thead>
            <tr>
              <th scope="col">Guest</th>
              <th scope="col">Room</th>
              <th scope="col">Stay</th>
              <th scope="col">Status</th>
              <th scope="col">
                <span className="visually-hidden">Link</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {stays.map((stay) => (
              <tr key={stay.id}>
                <th scope="row">{name(stay)}</th>
                <td>{stay.unitNumber ?? "—"}</td>
                <td>
                  {stay.arrival} to {stay.departure}
                </td>
                <td>{statuses[stay.status]}</td>
                <td>
                  <button type="button" onClick={() => void create(stay)}>
                    {stay.hasGuestLink ? "New link" : "Make link"}
                  </button>{" "}
                  {stay.hasGuestLink && (
                    <button type="button" onClick={() => void revoke(stay)}>
                      Revoke
                    </button>
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

function name(stay: Stay): string {
  return stay.guestName ?? "the guest";
}
