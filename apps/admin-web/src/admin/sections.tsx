import type { ApiClient, Schema } from "@hospitalityos/api-client";
import { useCallback } from "react";
import { hoursText } from "./hours";
import { HoursEditor } from "./HoursEditor";
import { ifMatch, optional } from "./outcome";
import { Records, type Action, type Field } from "./Records";

export interface SectionProps {
  client: ApiClient;
  propertyId: string;
  /** Whether the caller may change records (administrators); others read. */
  canEdit: boolean;
  /** The property's spaces, amenities, restaurants, and services by ID, to name what records refer to. */
  names: ReadonlyMap<string, string>;
}

const text = (name: string, label: string, required = false): Field => ({ name, label, kind: "text", required });
const area = (name: string, label: string): Field => ({ name, label, kind: "textarea" });
const local = (value: string) => value.slice(0, 16).replace("T", " ");
const named = (names: ReadonlyMap<string, string>, id: string | null | undefined) => (id ? (names.get(id) ?? id) : null);
const about = (names: ReadonlyMap<string, string>, subject: Schema<"SubjectReference"> | null) =>
  subject ? `${String(subject.kind)}${subject.id ? `: ${named(names, subject.id) ?? ""}` : ""}` : "The whole property";

export function PropertySection({ client, propertyId, canEdit }: SectionProps) {
  const load = useCallback(async () => {
    const { data } = await client.GET("/api/v1/properties/{id}", { params: { path: { id: propertyId } } });

    return data ? [data] : undefined;
  }, [client, propertyId]);

  return (
    <Records<Schema<"PropertyResponse">>
      title="Property"
      load={load}
      name={(property) => property.name}
      details={(property) => [
        ["Time zone", property.timeZone],
        ["Address", [property.address.line1, property.address.line2, property.address.city, property.address.postalCode, property.address.countryCode].filter(Boolean).join(", ")],
        ["Phone", property.contactPhone],
        ["Email", property.contactEmail],
      ]}
      form={
        canEdit
          ? {
              fields: [text("name", "Name", true), text("contactPhone", "Phone"), text("contactEmail", "Email")],
              values: (property) => ({ name: property.name, contactPhone: property.contactPhone ?? "", contactEmail: property.contactEmail ?? "" }),
              save: (property, values) =>
                client.PUT("/api/v1/properties/{id}", {
                  params: { path: { id: property.id } },
                  headers: ifMatch(property.version),
                  body: {
                    name: optional(values.name),
                    timeZone: property.timeZone,
                    address: property.address,
                    contactPhone: optional(values.contactPhone),
                    contactEmail: optional(values.contactEmail),
                  },
                }),
            }
          : undefined
      }
    />
  );
}

export function SpacesSection({ client, propertyId, canEdit, names }: SectionProps) {
  const load = useCallback(async () => (await client.GET("/api/v1/spaces", { params: { query: { propertyId } } })).data, [client, propertyId]);

  return (
    <Records<Schema<"SpaceResponse">>
      title="Spaces"
      load={load}
      name={(space) => space.name}
      summary={(space) => String(space.kind)}
      details={(space) => [
        ["Kind", String(space.kind)],
        ["Code", space.code],
        ["Inside", named(names, space.parentSpaceId)],
        ["Description", space.description],
        ["Directions", space.directions],
      ]}
      form={
        canEdit
          ? {
              fields: [text("name", "Name", true), text("code", "Code"), area("description", "Description"), area("directions", "Directions")],
              values: (space) => ({ name: space.name, code: space.code ?? "", description: space.description ?? "", directions: space.directions ?? "" }),
              save: (space, values) =>
                client.PUT("/api/v1/spaces/{id}", {
                  params: { path: { id: space.id } },
                  headers: ifMatch(space.version),
                  body: {
                    name: optional(values.name),
                    kind: space.kind,
                    code: optional(values.code),
                    description: optional(values.description),
                    directions: optional(values.directions),
                  },
                }),
            }
          : undefined
      }
    />
  );
}

export function AmenitiesSection({ client, propertyId, canEdit, names }: SectionProps) {
  const load = useCallback(async () => (await client.GET("/api/v1/amenities", { params: { query: { propertyId } } })).data, [client, propertyId]);

  return (
    <Records<Schema<"AmenityResponse">>
      title="Amenities"
      load={load}
      name={(amenity) => amenity.name}
      summary={(amenity) => String(amenity.category)}
      details={(amenity) => [
        ["Category", String(amenity.category)],
        ["Where", named(names, amenity.locationSpaceId)],
        ["Description", amenity.description],
      ]}
      form={
        canEdit
          ? {
              fields: [text("name", "Name", true), area("description", "Description")],
              values: (amenity) => ({ name: amenity.name, description: amenity.description ?? "" }),
              save: (amenity, values) =>
                client.PUT("/api/v1/amenities/{id}", {
                  params: { path: { id: amenity.id } },
                  headers: ifMatch(amenity.version),
                  body: {
                    name: optional(values.name),
                    category: amenity.category,
                    description: optional(values.description),
                    ...(amenity.locationSpaceId ? { locationSpaceId: amenity.locationSpaceId } : {}),
                  },
                }),
            }
          : undefined
      }
    />
  );
}

export function RestaurantsSection({ client, propertyId, canEdit, names }: SectionProps) {
  const load = useCallback(async () => (await client.GET("/api/v1/restaurants", { params: { query: { propertyId } } })).data, [client, propertyId]);

  return (
    <Records<Schema<"RestaurantResponse">>
      title="Restaurants"
      load={load}
      name={(restaurant) => restaurant.name}
      summary={(restaurant) => restaurant.cuisine ?? ""}
      details={(restaurant) => [
        ["Cuisine", restaurant.cuisine],
        ["Where", named(names, restaurant.locationSpaceId)],
        ["Description", restaurant.description],
      ]}
      form={
        canEdit
          ? {
              fields: [text("name", "Name", true), text("cuisine", "Cuisine"), area("description", "Description")],
              values: (restaurant) => ({ name: restaurant.name, cuisine: restaurant.cuisine ?? "", description: restaurant.description ?? "" }),
              save: (restaurant, values) =>
                client.PUT("/api/v1/restaurants/{id}", {
                  params: { path: { id: restaurant.id } },
                  headers: ifMatch(restaurant.version),
                  body: {
                    name: optional(values.name),
                    cuisine: optional(values.cuisine),
                    description: optional(values.description),
                    ...(restaurant.locationSpaceId ? { locationSpaceId: restaurant.locationSpaceId } : {}),
                  },
                }),
            }
          : undefined
      }
    />
  );
}

export function MenusSection({ client, propertyId, canEdit, names }: SectionProps) {
  // Menus are listed by restaurant.
  const load = useCallback(async () => {
    const restaurants = (await client.GET("/api/v1/restaurants", { params: { query: { propertyId } } })).data;

    if (!restaurants) {
      return undefined;
    }

    const menus = await Promise.all(restaurants.map((restaurant) => client.GET("/api/v1/menus", { params: { query: { restaurantId: restaurant.id } } })));

    return menus.every((menu) => menu.data) ? menus.flatMap((menu) => menu.data ?? []) : undefined;
  }, [client, propertyId]);

  return (
    <Records<Schema<"MenuResponse">>
      title="Menus"
      load={load}
      name={(menu) => menu.name}
      summary={(menu) => named(names, menu.restaurantId) ?? ""}
      details={(menu) => [
        ["Restaurant", named(names, menu.restaurantId)],
        ["Description", menu.description],
      ]}
      extra={(menu, reload) => (
        <table className="items">
          <caption>Items</caption>
          <thead>
            <tr>
              <th scope="col">Item</th>
              <th scope="col">Price</th>
              <th scope="col">Dietary</th>
              <th scope="col">Available</th>
            </tr>
          </thead>
          <tbody>
            {menu.items.map((item) => (
              <tr key={item.id}>
                <td>{item.name}</td>
                <td>{item.price ? `${item.price.currencyCode} ${item.price.amount.toFixed(2)}` : "—"}</td>
                <td>{item.dietary.join(", ") || "—"}</td>
                <td>
                  {canEdit ? (
                    <button
                      type="button"
                      aria-label={`${item.name}: ${item.isAvailable ? "mark sold out" : "mark available"}`}
                      onClick={() =>
                        void client
                          .PUT("/api/v1/menus/{id}/items/{itemId}/availability", {
                            params: { path: { id: menu.id, itemId: item.id } },
                            headers: ifMatch(menu.version),
                            body: { isAvailable: !item.isAvailable },
                          })
                          .then(reload)
                      }
                    >
                      {item.isAvailable ? "Available" : "Sold out"}
                    </button>
                  ) : item.isAvailable ? (
                    "Available"
                  ) : (
                    "Sold out"
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      form={
        canEdit
          ? {
              fields: [text("name", "Name", true), area("description", "Description")],
              values: (menu) => ({ name: menu.name, description: menu.description ?? "" }),
              save: (menu, values) =>
                client.PUT("/api/v1/menus/{id}", {
                  params: { path: { id: menu.id } },
                  headers: ifMatch(menu.version),
                  body: {
                    name: optional(values.name),
                    description: optional(values.description),
                    items: menu.items.map((item) => ({
                      id: item.id,
                      name: item.name,
                      description: item.description,
                      price: item.price,
                      category: item.category,
                      dietary: item.dietary,
                      isAvailable: item.isAvailable,
                    })),
                  },
                }),
            }
          : undefined
      }
    />
  );
}

export function EventsSection({ client, propertyId, canEdit, names }: SectionProps) {
  const load = useCallback(async () => (await client.GET("/api/v1/events", { params: { query: { propertyId } } })).data, [client, propertyId]);

  return (
    <Records<Schema<"PropertyEventResponse">>
      title="Events"
      load={load}
      name={(event) => event.title}
      summary={(event) => `${local(event.localStart)} · ${String(event.status)}`}
      details={(event) => [
        ["Status", String(event.status)],
        ["When", `${local(event.localStart)} to ${local(event.localEnd)}`],
        ["Where", named(names, event.locationSpaceId)],
        ["For", String(event.audience)],
        ["Price", event.price ? `${event.price.currencyCode} ${event.price.amount.toFixed(2)}` : "Free"],
        ["Booking", event.bookingRequired ? "Required" : "Not needed"],
        ["Description", event.description],
      ]}
      actions={(event): Action[] =>
        canEdit && event.status === "Scheduled"
          ? [{ label: "Cancel event", run: () => client.POST("/api/v1/events/{id}/cancel", { params: { path: { id: event.id } }, headers: ifMatch(event.version) }) }]
          : []
      }
      form={
        canEdit
          ? {
              fields: [
                text("title", "Title", true),
                { name: "localStart", label: "Starts", kind: "datetime", required: true },
                { name: "localEnd", label: "Ends", kind: "datetime", required: true },
                { name: "bookingRequired", label: "Booking required", kind: "checkbox" },
                area("description", "Description"),
              ],
              values: (event) => ({
                title: event.title,
                localStart: event.localStart.slice(0, 16),
                localEnd: event.localEnd.slice(0, 16),
                bookingRequired: event.bookingRequired,
                description: event.description ?? "",
              }),
              save: (event, values) =>
                client.PUT("/api/v1/events/{id}", {
                  params: { path: { id: event.id } },
                  headers: ifMatch(event.version),
                  body: {
                    title: optional(values.title),
                    category: event.category,
                    audience: event.audience,
                    localStart: `${String(values.localStart)}:00`,
                    localEnd: `${String(values.localEnd)}:00`,
                    description: optional(values.description),
                    ...(event.locationSpaceId ? { locationSpaceId: event.locationSpaceId } : {}),
                    price: event.price,
                    bookingRequired: values.bookingRequired === true,
                  },
                }),
            }
          : undefined
      }
    />
  );
}

export function ServicesSection({ client, propertyId, canEdit }: SectionProps) {
  const load = useCallback(async () => (await client.GET("/api/v1/services", { params: { query: { propertyId } } })).data, [client, propertyId]);

  return (
    <Records<Schema<"ServiceResponse">>
      title="Services"
      load={load}
      name={(service) => service.name}
      summary={(service) => service.code}
      details={(service) => [
        ["Code", service.code],
        ["Category", String(service.category)],
        ["Guests can ask for it", service.isGuestRequestable ? "Yes" : "No"],
        ["Target time", service.targetCompletion],
        ["Items", service.items.map((item) => `${item.name} (up to ${String(item.maxQuantityPerRequest)})`).join(", ") || null],
        ["Description", service.description],
      ]}
      form={
        canEdit
          ? {
              fields: [text("name", "Name", true), { name: "isGuestRequestable", label: "Guests can ask for it", kind: "checkbox" }, area("description", "Description")],
              values: (service) => ({ name: service.name, isGuestRequestable: service.isGuestRequestable, description: service.description ?? "" }),
              save: (service, values) =>
                client.PUT("/api/v1/services/{id}", {
                  params: { path: { id: service.id } },
                  headers: ifMatch(service.version),
                  body: {
                    code: service.code,
                    name: optional(values.name),
                    category: service.category,
                    description: optional(values.description),
                    isGuestRequestable: values.isGuestRequestable === true,
                    items: service.items.map((item) => ({ code: item.code, name: item.name, maxQuantityPerRequest: item.maxQuantityPerRequest })),
                    targetCompletion: service.targetCompletion,
                  },
                }),
            }
          : undefined
      }
    />
  );
}

export function PoliciesSection({ client, propertyId, canEdit, names }: SectionProps) {
  const load = useCallback(async () => (await client.GET("/api/v1/policies", { params: { query: { propertyId } } })).data, [client, propertyId]);

  return (
    <Records<Schema<"PolicyResponse">>
      title="Policies"
      load={load}
      name={(policy) => policy.title}
      summary={(policy) => `${String(policy.type)} · ${String(policy.status)}`}
      details={(policy) => [
        ["Status", String(policy.status)],
        ["Topic", String(policy.type)],
        ["Applies to", about(names, policy.appliesTo)],
        ["Policy", policy.content],
      ]}
      actions={(policy): Action[] =>
        !canEdit
          ? []
          : policy.status === "Draft"
            ? [{ label: "Activate", run: () => client.POST("/api/v1/policies/{id}/activate", { params: { path: { id: policy.id } }, headers: ifMatch(policy.version) }) }]
            : policy.status === "Active"
              ? [{ label: "Retire", run: () => client.POST("/api/v1/policies/{id}/retire", { params: { path: { id: policy.id } }, headers: ifMatch(policy.version) }) }]
              : []
      }
      form={
        canEdit
          ? {
              fields: [text("title", "Title", true), area("content", "Policy")],
              values: (policy) => ({ title: policy.title, content: policy.content }),
              save: (policy, values) =>
                client.PUT("/api/v1/policies/{id}", {
                  params: { path: { id: policy.id } },
                  headers: ifMatch(policy.version),
                  body: {
                    type: policy.type,
                    title: optional(values.title),
                    content: optional(values.content),
                    effectiveFrom: policy.effectiveFrom,
                    effectiveTo: policy.effectiveTo,
                    appliesTo: policy.appliesTo,
                  },
                }),
            }
          : undefined
      }
    />
  );
}

export function HoursSection({ client, propertyId, canEdit, names }: SectionProps) {
  const load = useCallback(async () => (await client.GET("/api/v1/operating-hours", { params: { query: { propertyId } } })).data, [client, propertyId]);

  return (
    <Records<Schema<"OperatingHoursResponse">>
      title="Operating hours"
      load={load}
      name={(hours) => about(names, hours.subject)}
      details={(hours) => [
        ["Weekly", hoursText(hours.weekly)],
        ["Exceptions", hours.overrides.map((day) => `${String(day.date)}: ${(day.intervals ?? []).map((interval) => `${String(interval.opens).slice(0, 5)}–${String(interval.closes).slice(0, 5)}`).join(", ") || "closed"}`).join("; ") || null],
        ["Closures", hours.closures.map((closure) => `${String(closure.from)} to ${String(closure.to)}`).join("; ") || null],
      ]}
      extra={(hours, reload) =>
        canEdit ? (
          <HoursEditor
            key={`${hours.id}:${String(hours.version)}`}
            weekly={hours.weekly}
            onSave={async (weekly) => {
              const outcome = await client.PUT("/api/v1/operating-hours/{id}", {
                params: { path: { id: hours.id } },
                headers: ifMatch(hours.version),
                body: { weekly, overrides: hours.overrides, closures: hours.closures },
              });
              await reload();

              return outcome;
            }}
          />
        ) : null
      }
    />
  );
}

export function ContentSection({ client, propertyId, canEdit, names }: SectionProps) {
  const load = useCallback(async () => (await client.GET("/api/v1/content", { params: { query: { propertyId } } })).data, [client, propertyId]);

  return (
    <Records<Schema<"ContentItemResponse">>
      title="Content"
      load={load}
      name={(item) => item.title}
      summary={(item) => `${String(item.type)} · ${String(item.status)}`}
      details={(item) => [
        ["Status", String(item.status)],
        ["Kind", String(item.type)],
        ["About", about(names, item.about)],
        ["Tags", item.tags.join(", ") || null],
        ["Text", item.body],
      ]}
      actions={(item): Action[] =>
        !canEdit
          ? []
          : item.status === "Draft"
            ? [{ label: "Publish", run: () => client.POST("/api/v1/content/{id}/publish", { params: { path: { id: item.id } }, headers: ifMatch(item.version) }) }]
            : item.status === "Published"
              ? [{ label: "Archive", run: () => client.POST("/api/v1/content/{id}/archive", { params: { path: { id: item.id } }, headers: ifMatch(item.version) }) }]
              : []
      }
      form={
        canEdit
          ? {
              fields: [text("title", "Title", true), area("body", "Text"), { name: "tags", label: "Tags", kind: "text", hint: "Separated by commas" }],
              values: (item) => ({ title: item.title, body: item.body, tags: item.tags.join(", ") }),
              save: (item, values) =>
                client.PUT("/api/v1/content/{id}", {
                  params: { path: { id: item.id } },
                  headers: ifMatch(item.version),
                  body: {
                    type: item.type,
                    title: optional(values.title),
                    body: optional(values.body),
                    about: item.about,
                    tags: String(values.tags)
                      .split(",")
                      .map((tag) => tag.trim())
                      .filter(Boolean),
                  },
                }),
            }
          : undefined
      }
    />
  );
}
