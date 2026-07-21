namespace Application.Expenses;

public enum ExpenseFailureReason
{
    None,
    AttachmentNotFound,
    AttachmentAlreadyLinked,
    AttachmentNotOwned,
    AmountNotPositive,
    ExpenseDateInFuture,
    ExpenseNotFound,
    NotOwner,
    NotDraft,
    CurrencyInvalid,
    DescriptionTooLong,
    NotEditable,
    NotCancellable,
}
