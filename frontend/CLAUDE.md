@../AGENTS.md

Frontend-specific rules for `frontend/`. Stack and feature layout are in the root `AGENTS.md` —
this file covers only how to work inside this project.

## Commands (run from `frontend/`)

```bash
pnpm dev                 # Vite dev server
pnpm build                # production build
pnpm test                 # Vitest component tests
pnpm test -- --watch      # watch mode
pnpm lint                 # oxlint
pnpm exec tsc --noEmit    # type-check only
npx playwright test       # E2E (repo/workspace root)
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
