import type { ApiClient } from "@hospitalityos/api-client";
import { useEffect, useState, type ComponentType } from "react";
import { Access } from "./Access";
import { Integrations } from "./Integrations";
import {
  AmenitiesSection,
  ContentSection,
  EventsSection,
  HoursSection,
  MenusSection,
  PoliciesSection,
  PropertySection,
  RestaurantsSection,
  ServicesSection,
  SpacesSection,
  type SectionProps,
} from "./sections";
import { FailedEvents } from "./FailedEvents";
import { Traces } from "./Traces";

/**
 * The sections of spec §34, in its order, then failed events (L1), and who may sign in. People & access, integrations,
 * and failed events are for administrators only; everything else is read by managers too, and changed only by
 * administrators.
 */
const sections: readonly { id: string; title: string; adminOnly?: boolean; component: ComponentType<SectionProps> }[] = [
  { id: "property", title: "Property", component: PropertySection },
  { id: "spaces", title: "Spaces", component: SpacesSection },
  { id: "amenities", title: "Amenities", component: AmenitiesSection },
  { id: "restaurants", title: "Restaurants", component: RestaurantsSection },
  { id: "menus", title: "Menus", component: MenusSection },
  { id: "events", title: "Events", component: EventsSection },
  { id: "services", title: "Services", component: ServicesSection },
  { id: "policies", title: "Policies", component: PoliciesSection },
  { id: "hours", title: "Operating Hours", component: HoursSection },
  { id: "content", title: "Content", component: ContentSection },
  { id: "access", title: "People & access", adminOnly: true, component: ({ client, propertyId }) => <Access client={client} propertyId={propertyId} /> },
  { id: "integrations", title: "Integrations", adminOnly: true, component: ({ client, propertyId }) => <Integrations client={client} propertyId={propertyId} /> },
  { id: "traces", title: "AI Traces", component: ({ client, propertyId }) => <Traces client={client} propertyId={propertyId} /> },
  { id: "failed-events", title: "Failed events", adminOnly: true, component: ({ client }) => <FailedEvents client={client} /> },
];

export interface AdminProps {
  client: ApiClient;
  propertyId: string;
  /** Whether the caller is an administrator, who may change records; managers read. */
  isAdmin: boolean;
}

/**
 * The admin surface (J4, spec §34): one section of the property's configuration, or its integrations, or the AI trace
 * viewer, at a time; the section is kept in the address, so a reload or a link opens it again.
 */
export function Admin({ client, propertyId, isAdmin }: AdminProps) {
  const shown = sections.filter((section) => isAdmin || !section.adminOnly);
  const [current, setCurrent] = useState(() => shown.find((section) => `#${section.id}` === location.hash)?.id ?? "property");
  const names = useNames(client, propertyId);
  const Section = (shown.find((section) => section.id === current) ?? shown[0])?.component;

  function go(id: string) {
    setCurrent(id);
    history.replaceState(null, "", `#${id}`);
  }

  return (
    <div className="admin">
      <nav aria-label="Sections" className="nav-pane">
        <ul>
          {shown.map((section) => (
            <li key={section.id}>
              <a href={`#${section.id}`} aria-current={section.id === current ? "page" : undefined} onClick={(event) => {
                event.preventDefault();
                go(section.id);
              }}>
                {section.title}
              </a>
            </li>
          ))}
        </ul>
      </nav>
      <div className="content card">{Section && <Section key={current} client={client} propertyId={propertyId} canEdit={isAdmin} names={names} />}</div>
    </div>
  );
}

/**
 * What records refer to by ID, named: the property's spaces, amenities, restaurants, and services.
 */
function useNames(client: ApiClient, propertyId: string): ReadonlyMap<string, string> {
  const [names, setNames] = useState<ReadonlyMap<string, string>>(new Map());

  useEffect(() => {
    let current = true;
    const query = { params: { query: { propertyId } } };

    void Promise.all([
      client.GET("/api/v1/spaces", query),
      client.GET("/api/v1/amenities", query),
      client.GET("/api/v1/restaurants", query),
      client.GET("/api/v1/services", query),
    ]).then(([spaces, amenities, restaurants, services]) => {
      if (current) {
        setNames(
          new Map([
            ...(spaces.data ?? []).map((space) => [space.id, space.name] as const),
            ...(amenities.data ?? []).map((amenity) => [amenity.id, amenity.name] as const),
            ...(restaurants.data ?? []).map((restaurant) => [restaurant.id, restaurant.name] as const),
            ...(services.data ?? []).map((service) => [service.id, service.name] as const),
          ]),
        );
      }
    });

    return () => {
      current = false;
    };
  }, [client, propertyId]);

  return names;
}
