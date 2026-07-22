export type EmployeeRole = 'Employee' | 'Manager' | 'Finance' | 'ComplianceOfficer'

export interface User {
  id: string
  email: string
  employeeNumber: string
  firstName: string
  lastName: string
  role: EmployeeRole
}

export interface AuthTokens {
  accessToken: string
  refreshToken: string
}
