import { Link, useNavigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { useBreadcrumbStore } from '@/store/breadcrumbStore'

export function Breadcrumbs() {
  const role = useAuthStore((state) => state.user?.role)
  const trail = useBreadcrumbStore((state) => state.trail)
  const truncateTo = useBreadcrumbStore((state) => state.truncateTo)
  const navigate = useNavigate()

  const homePath = role === 'ComplianceOfficer' ? '/expenses' : '/dashboard'

  function handleCrumbClick(index: number) {
    const path = trail[index].path
    truncateTo(index)
    navigate(path)
  }

  return (
    <nav aria-label="Breadcrumb" className="flex flex-wrap items-center gap-1 border-b p-4 text-sm text-muted-foreground">
      <Link
        to={homePath}
        className="hover:text-foreground hover:underline"
        onClick={() => truncateTo(-1)}
      >
        Home
      </Link>
      {trail.map((entry, index) => (
        <span key={`${entry.path}-${index}`} className="flex items-center gap-1">
          <span aria-hidden="true">/</span>
          {index === trail.length - 1 ? (
            <span className="text-foreground">{entry.label}</span>
          ) : (
            <button
              type="button"
              onClick={() => handleCrumbClick(index)}
              className="hover:text-foreground hover:underline"
            >
              {entry.label}
            </button>
          )}
        </span>
      ))}
    </nav>
  )
}
