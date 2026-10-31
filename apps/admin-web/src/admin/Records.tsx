import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";
import { describe, type Outcome } from "./outcome";

export type Values = Record<string, string | boolean>;

/**
 * One field of an edit form.
 */
export interface Field {
  name: string;
  label: string;
  kind: "text" | "textarea" | "checkbox" | "datetime";
  required?: boolean | undefined;
  hint?: string | undefined;
}

/**
 * A step a record's state allows, e.g. publishing a draft.
 */
export interface Action {
  label: string;
  run: () => Promise<Outcome>;
}

export interface RecordsProps<T extends { id: string; version: number }> {
  title: string;
  /** Reads the records; `undefined` when they could not be read. */
  load: () => Promise<T[] | undefined>;
  name: (record: T) => string;
  summary?: ((record: T) => string) | undefined;
  details: (record: T) => [string, ReactNode][];
  /** Editing, for those who may: the form's fields, their values from the record, and how a change is saved. */
  form?:
    | {
        fields: Field[];
        values: (record: T) => Values;
        save: (record: T, values: Values) => Promise<Outcome>;
      }
    | undefined;
  actions?: ((record: T) => Action[]) | undefined;
  /** More about the record below its details, e.g. a menu's items; `reload` reads the records again after a change. */
  extra?: ((record: T, reload: () => Promise<void>) => ReactNode) | undefined;
}

/**
 * A section of the admin surface (spec §34): the property's records of one kind, one of them open with its details, and,
 * for administrators, a form to change it and the steps its state allows. Every change is sent with the version it was
 * made on, so a stale one is refused and the records are read again.
 */
export function Records<T extends { id: string; version: number }>({ title, load, name, summary, details, form, actions, extra }: RecordsProps<T>) {
  const [records, setRecords] = useState<T[] | undefined>();
  const [open, setOpen] = useState<string | undefined>();
  const [problem, setProblem] = useState<string | undefined>();
  const [saved, setSaved] = useState<string | undefined>();

  const reload = useCallback(async () => {
    const read = await load();

    setRecords(read);
    setProblem(read ? undefined : `The ${title.toLowerCase()} could not be read.`);
  }, [load, title]);

  useEffect(() => {
    // Loading sets state once the API answers.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void reload();
  }, [reload]);

  async function change(run: () => Promise<Outcome>, done: string) {
    const outcome = await run();

    // Read again first, so what the change came to is what stays shown.
    await reload();
    setProblem(outcome.error ? describe(outcome.error) : undefined);
    setSaved(outcome.error ? undefined : done);
  }

  const selected = records?.find((record) => record.id === open);

  return (
    <section aria-labelledby="section" className="records">
      <h2 id="section">{title}</h2>
      {problem && <p role="alert">{problem}</p>}
      {saved && <p role="status">{saved}</p>}
      {records?.length === 0 && <p>None yet.</p>}
      <ul className="list">
        {records?.map((record) => (
          <li key={record.id}>
            <button
              type="button"
              aria-pressed={record.id === open}
              onClick={() => {
                setOpen(record.id === open ? undefined : record.id);
                setSaved(undefined);
              }}
            >
              <strong>{name(record)}</strong>
              {summary && <span>{summary(record)}</span>}
            </button>
          </li>
        ))}
      </ul>
      {selected && (
        <article aria-label={name(selected)} className="record">
          <h3>{name(selected)}</h3>
          <dl>
            {details(selected).map(([label, value]) => (
              <div key={label}>
                <dt>{label}</dt>
                <dd>{value ?? "—"}</dd>
              </div>
            ))}
          </dl>
          {actions && actions(selected).length > 0 && (
            <p className="actions">
              {actions(selected).map((action) => (
                <button key={action.label} type="button" onClick={() => void change(action.run, `${action.label}: done.`)}>
                  {action.label}
                </button>
              ))}
            </p>
          )}
          {extra?.(selected, reload)}
          {form && (
            // Keyed by version, so the form starts again from the record as it now stands after every change.
            <EditForm
              key={`${selected.id}:${String(selected.version)}`}
              fields={form.fields}
              initial={form.values(selected)}
              onSave={(values) => change(() => form.save(selected, values), "Saved.")}
            />
          )}
        </article>
      )}
    </section>
  );
}

function EditForm({ fields, initial, onSave }: { fields: Field[]; initial: Values; onSave: (values: Values) => Promise<void> }) {
  const [values, setValues] = useState<Values>(initial);
  const [saving, setSaving] = useState(false);
  const set = (name: string, value: string | boolean) => setValues((now) => ({ ...now, [name]: value }));
  const missing = fields.some((field) => field.required && !String(values[field.name] ?? "").trim());

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSaving(true);

    try {
      await onSave(values);
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={(event) => void submit(event)} className="edit">
      <h4>Edit</h4>
      {fields.map((field) => {
        const id = `field-${field.name}`;
        const value = values[field.name];

        return (
          <p key={field.name}>
            <label htmlFor={id}>{field.label}</label>
            {field.kind === "textarea" ? (
              <textarea id={id} rows={4} value={String(value ?? "")} onChange={(event) => set(field.name, event.target.value)} />
            ) : field.kind === "checkbox" ? (
              <input id={id} type="checkbox" checked={value === true} onChange={(event) => set(field.name, event.target.checked)} />
            ) : (
              <input
                id={id}
                type={field.kind === "datetime" ? "datetime-local" : "text"}
                value={String(value ?? "")}
                onChange={(event) => set(field.name, event.target.value)}
              />
            )}
            {field.hint && <small>{field.hint}</small>}
          </p>
        );
      })}
      <button type="submit" disabled={saving || missing}>
        Save
      </button>
    </form>
  );
}
