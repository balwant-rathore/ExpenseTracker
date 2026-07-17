## REMOVED Requirements

### Requirement: Seed Runner Scaffold
**Reason**: Superseded by the real Employee CSV import — the ET001 no-op scaffold no longer
describes any real behavior in the system.
**Migration**: See the ADDED "Employee CSV Seed Import" requirement below for the real import
behavior that replaces it.

## ADDED Requirements

### Requirement: Employee CSV Seed Import
The backend SHALL implement the `ISeedRunner` abstraction (defined in ET001) to perform the real
`Employee` CSV import from `docs/EmployeeSeedData.csv` (`docs/FRS.md` §14, `docs/SDS.md` §3.11):
insert `Employee` rows whose `EmployeeNumber` is not already present in the database (idempotent
skip of existing records), then resolve each inserted row's `ManagerId` by looking up its CSV
`Manager` value against imported `EmployeeNumber`s in a second pass. An unresolvable `Manager`
reference SHALL be left `null` on that row and logged as a warning rather than failing the run.
The import SHALL continue to execute automatically at Development startup.

#### Scenario: Seed runner imports new Employee rows and skips existing ones
- **WHEN** the application starts in the Development environment and `docs/EmployeeSeedData.csv`
  contains rows whose EmployeeNumber does not yet exist in the database
- **THEN** the seed runner inserts those rows, and any row whose EmployeeNumber already exists in
  the database is left unmodified

#### Scenario: Seed runner resolves manager references after insert
- **WHEN** all CSV rows have been inserted and a row's Manager column references another row's
  EmployeeNumber
- **THEN** that row's ManagerId SHALL be set to the referenced Employee's EmployeeId, regardless
  of the CSV's row order

#### Scenario: Unresolvable manager reference is skipped with a warning
- **WHEN** a row's Manager column references an EmployeeNumber that does not exist among the
  imported rows
- **THEN** that row's ManagerId SHALL remain null, a warning SHALL be logged, and the import
  SHALL continue processing remaining rows
