namespace Domain.Repositories;

public enum ExpenseInsertOutcome
{
    Success,
    ExpenseNumberConflict,
    AttachmentAlreadyLinked,
}
