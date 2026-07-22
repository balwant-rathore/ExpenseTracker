/**
 * Case-insensitive membership check against a backend `fields` array.
 *
 * The backend's `VALIDATION_ERROR` responses don't use one consistent casing
 * for field names: FluentValidation-driven failures return the C# property
 * name verbatim (e.g. "Email"), while a couple of service-level checks
 * hardcode lowerCamelCase (e.g. "password", "newPassword" — see
 * AuthController.FailureResult). Matching case-insensitively avoids the
 * frontend silently failing to highlight a field just because of that
 * backend inconsistency.
 */
export function hasFieldError(fields: string[] | undefined, fieldName: string): boolean {
  if (!fields) {
    return false
  }
  return fields.some((field) => field.toLowerCase() === fieldName.toLowerCase())
}
