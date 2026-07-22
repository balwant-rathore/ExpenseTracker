import { describe, expect, it } from 'vitest'
import { getApplicableReviewActions, isExpenseOwner } from './reviewEligibility'

describe('isExpenseOwner', () => {
  it('is true when employeeNumber matches the current user', () => {
    expect(isExpenseOwner({ employeeNumber: 'EMP001' }, 'EMP001')).toBe(true)
  })

  it('is false when employeeNumber does not match', () => {
    expect(isExpenseOwner({ employeeNumber: 'EMP001' }, 'EMP002')).toBe(false)
  })

  it('is false when the current user is undefined', () => {
    expect(isExpenseOwner({ employeeNumber: 'EMP001' }, undefined)).toBe(false)
  })
})

describe('getApplicableReviewActions', () => {
  it('returns Manager approve/reject for a non-owner Manager on a Submitted expense', () => {
    const actions = getApplicableReviewActions(
      { status: 'Submitted', category: 'Travel' },
      'Manager',
      false,
    )
    expect(actions).toEqual([{ type: 'managerApprove' }, { type: 'managerReject' }])
  })

  it('returns nothing for a Manager on a non-Submitted expense', () => {
    const actions = getApplicableReviewActions(
      { status: 'Approved', category: 'Travel' },
      'Manager',
      false,
    )
    expect(actions).toEqual([])
  })

  it('returns Compliance approve/reject for an Approved ClientEntertainment expense', () => {
    const actions = getApplicableReviewActions(
      { status: 'Approved', category: 'ClientEntertainment' },
      'ComplianceOfficer',
      false,
    )
    expect(actions).toEqual([{ type: 'complianceApprove' }, { type: 'complianceReject' }])
  })

  it('returns nothing for Compliance on a non-ClientEntertainment category', () => {
    const actions = getApplicableReviewActions(
      { status: 'Approved', category: 'Travel' },
      'ComplianceOfficer',
      false,
    )
    expect(actions).toEqual([])
  })

  it('returns nothing for Compliance on a ClientEntertainment expense that is not Approved', () => {
    const actions = getApplicableReviewActions(
      { status: 'Submitted', category: 'ClientEntertainment' },
      'ComplianceOfficer',
      false,
    )
    expect(actions).toEqual([])
  })

  it('returns Finance reimburse for an Approved non-ClientEntertainment expense', () => {
    const actions = getApplicableReviewActions(
      { status: 'Approved', category: 'Travel' },
      'Finance',
      false,
    )
    expect(actions).toEqual([{ type: 'financeReimburse' }])
  })

  it('returns Finance reimburse for a ComplianceApproved ClientEntertainment expense', () => {
    const actions = getApplicableReviewActions(
      { status: 'ComplianceApproved', category: 'ClientEntertainment' },
      'Finance',
      false,
    )
    expect(actions).toEqual([{ type: 'financeReimburse' }])
  })

  it('returns nothing for Finance on an Approved (not yet Compliance Approved) ClientEntertainment expense', () => {
    const actions = getApplicableReviewActions(
      { status: 'Approved', category: 'ClientEntertainment' },
      'Finance',
      false,
    )
    expect(actions).toEqual([])
  })

  it('returns nothing for the owner regardless of role or status', () => {
    const actions = getApplicableReviewActions(
      { status: 'Submitted', category: 'Travel' },
      'Manager',
      true,
    )
    expect(actions).toEqual([])
  })

  it('returns nothing when role is undefined', () => {
    const actions = getApplicableReviewActions(
      { status: 'Submitted', category: 'Travel' },
      undefined,
      false,
    )
    expect(actions).toEqual([])
  })
})
