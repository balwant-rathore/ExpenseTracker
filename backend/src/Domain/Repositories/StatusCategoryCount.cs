using Domain.Enums;

namespace Domain.Repositories;

public record StatusCategoryCount(ExpenseStatus Status, ExpenseCategory Category, int Count);
