import { create } from 'zustand'

export interface BreadcrumbEntry {
  label: string
  path: string
}

interface BreadcrumbState {
  trail: BreadcrumbEntry[]
  push: (entry: BreadcrumbEntry) => void
  truncateTo: (index: number) => void
  reset: () => void
}

export const useBreadcrumbStore = create<BreadcrumbState>((set) => ({
  trail: [],
  push: (entry) =>
    set((state) => {
      const last = state.trail[state.trail.length - 1]
      if (last?.path === entry.path) {
        // Same route already at the top of the trail — replace its label rather than no-op,
        // so a page whose label depends on fetched data (e.g. an expense number) can correct
        // an initial placeholder once the data resolves, without appending a duplicate entry.
        return last.label === entry.label ? state : { trail: [...state.trail.slice(0, -1), entry] }
      }
      return { trail: [...state.trail, entry] }
    }),
  truncateTo: (index) => set((state) => ({ trail: state.trail.slice(0, index + 1) })),
  reset: () => set({ trail: [] }),
}))
