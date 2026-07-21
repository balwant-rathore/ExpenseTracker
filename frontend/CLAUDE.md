@../AGENTS.md

Frontend-specific rules for `frontend/`. Stack and feature layout are in the root `AGENTS.md` —
this file covers only how to work inside this project.

## Commands (run from `frontend/`)

Standard scripts (`dev`, `build`, `test`, `lint`) are in `package.json`. Non-obvious ones:

```bash
pnpm test -- --watch      # watch mode
pnpm exec tsc --noEmit    # type-check only (not a package.json script)
npx playwright test       # E2E — run from repo/workspace root, not frontend/
```

## Component & State Management Patterns

- One component per file, colocated with its test (`ExpenseForm.tsx` +
  `ExpenseForm.test.tsx`) inside the owning `features/<name>/` folder.
- Server data (expenses, dashboard, users) is fetched and cached exclusively through TanStack
  Query hooks in `features/<name>/api/` or `src/api/` — components never call `fetch`/`axios`
  directly.
- Zustand stores hold only ephemeral UI state (e.g. sidebar open, active tab, wizard step) —
  if a value comes from or must sync back to the server, it belongs in a Query hook, not a store.
- Forms use React Hook Form + a Zod schema per form; the Zod schema mirrors backend validation for
  UX only — never treat a passed client check as authoritative.
- Route-level data loading happens in `pages/`, which compose `features/` components; `features/`
  components stay route-agnostic and reusable.
- Shared, cross-feature UI (buttons, dialogs, inputs) lives in `components/`; anything specific to
  one feature stays inside that `features/<name>/` folder.

## Anti-Patterns to Avoid

- Don't duplicate backend business-rule validation beyond what's needed for immediate UX feedback
  (e.g. required-field, format) — workflow/authorization rules are not the frontend's job.
- Don't store TanStack Query data inside a Zustand store "for convenience" — it will desync from
  cache invalidation/refetches.
- Don't call the workflow action endpoints (`/submit`, `/approve`, etc.) with an optimistic status
  update that isn't reconciled against the server response — always invalidate/refetch on success.
- Don't fetch data with `useEffect` + `useState` — use a TanStack Query hook instead.
- Don't prop-drill auth/role state through many layers — read it from the auth hook/store at the
  point of use.
- Don't hardcode role-based UI branching in multiple components — centralize role checks in one
  place (hook or utility) that pages/components call.

## Gotchas to Watch For (see backend/CLAUDE.md's "Gotchas" for the ET007 incident these
generalize from — no shared code exists per AGENTS.md §12, so nothing here is type-checked
against the backend automatically)

- **A Zod schema that "looks equivalent" to the backend DTO can still diverge from it silently.**
  Since frontend and backend share no code or types, there is no compiler check that a form's Zod
  schema/TypeScript interface actually matches the current shape of the backend request/response
  DTO it targets. Whenever a backend DTO's field changes — nullability, which fields are
  genuinely mandatory vs. defaulted server-side, an enum becoming a string — re-check the
  corresponding Zod schema and TS type field-by-field against the actual current backend code
  (or `docs/SDS.md` §5 if it's been kept current), not against memory of what it used to be.
- **When a design says two flows share the "same validation" (e.g., the create form and the edit
  form both enforce a field the same way), implement and verify both explicitly.** Don't assume
  that reusing one shared form component automatically keeps every field's validation in sync
  between flows — check each flow's actual rendered behavior, not just that the component is
  shared.
- **A backend field that's optional/nullable is not automatically safe to treat as always-present
  in a TypeScript type.** If the backend marks a response field nullable (or a request field
  becomes optional server-side, falling back to a business-rule check instead of a hard
  requirement), the frontend type must reflect that (`string | null`, not `string`) — TypeScript
  won't catch this drift for you since there's no shared/generated type between the two apps.
