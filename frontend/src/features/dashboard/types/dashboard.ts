export interface EmployeeDashboard {
  totalSubmitted: number
  approved: number
  reimbursed: number
}

export interface ManagerDashboard extends EmployeeDashboard {
  pendingApprovals: number
}

export interface FinanceDashboard extends ManagerDashboard {
  pendingReimbursements: number
}

export type DashboardResponse = EmployeeDashboard | ManagerDashboard | FinanceDashboard
