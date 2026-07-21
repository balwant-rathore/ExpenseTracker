using Api.Authentication;
using Api.Authorization;
using Application.Expenses;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.ErrorHandling;

namespace Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly IValidator<CreateExpenseRequest> _createValidator;
    private readonly IValidator<UpdateExpenseRequest> _updateValidator;

    public ExpensesController(
        IExpenseService expenseService,
        IValidator<CreateExpenseRequest> createValidator,
        IValidator<UpdateExpenseRequest> updateValidator)
    {
        _expenseService = expenseService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var result = await _expenseService.CreateAsync(User.GetEmployeeId(), request, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return StatusCode(StatusCodes.Status201Created, new ExpenseEnvelopeResponse(result.Expense!));
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _expenseService.SubmitAsync(User.GetEmployeeId(), id, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return Ok(new ExpenseEnvelopeResponse(result.Expense!));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _expenseService.GetByIdAsync(User.GetEmployeeId(), id, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return Ok(new ExpenseEnvelopeResponse(result.Expense!));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var result = await _expenseService.UpdateAsync(User.GetEmployeeId(), id, request, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return Ok(new ExpenseEnvelopeResponse(result.Expense!));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _expenseService.CancelAsync(User.GetEmployeeId(), id, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return Ok(new ExpenseEnvelopeResponse(result.Expense!));
    }

    private IActionResult ValidationErrorResult(FluentValidation.Results.ValidationResult validation)
    {
        var fields = validation.Errors.Select(e => e.PropertyName).Distinct().ToList();
        return BadRequest(new ErrorResponse(new ErrorDetail(
            "VALIDATION_ERROR",
            "One or more fields are invalid.",
            fields,
            HttpContext.TraceIdentifier)));
    }

    private IActionResult FailureResult(ExpenseFailureReason reason)
    {
        return reason switch
        {
            ExpenseFailureReason.AttachmentNotFound => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "The referenced attachment could not be found.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.AttachmentAlreadyLinked => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "The referenced attachment is already linked to another expense.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.AttachmentNotOwned => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "The referenced attachment was not uploaded by you.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.AmountNotPositive => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "Amount must be greater than zero.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.ExpenseDateInFuture => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "Expense date cannot be in the future.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.ExpenseNotFound => NotFound(new ErrorResponse(new ErrorDetail(
                "RESOURCE_NOT_FOUND",
                "Expense not found.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.NotOwner => StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse(new ErrorDetail(
                "AUTHORIZATION_FAILED",
                "You do not have permission to perform this action.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.NotDraft => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "Only expenses in Draft status can be submitted.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.CurrencyInvalid => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "Currency must be INR.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.DescriptionTooLong => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "Description must not exceed 500 characters.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.NotEditable => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "Only expenses in Draft or Submitted status can be edited.",
                [],
                HttpContext.TraceIdentifier))),
            ExpenseFailureReason.NotCancellable => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "Only expenses in Draft or Submitted status can be cancelled.",
                [],
                HttpContext.TraceIdentifier))),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
