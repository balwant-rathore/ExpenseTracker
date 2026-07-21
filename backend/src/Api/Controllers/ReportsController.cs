using Api.Authorization;
using Application.Reports;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.ErrorHandling;

namespace Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = AuthorizationPolicyNames.Finance)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IValidator<MonthlyReimbursementQuery> _monthlyReimbursementValidator;

    public ReportsController(
        IReportService reportService,
        IValidator<MonthlyReimbursementQuery> monthlyReimbursementValidator)
    {
        _reportService = reportService;
        _monthlyReimbursementValidator = monthlyReimbursementValidator;
    }

    [HttpGet("monthly-reimbursement")]
    public async Task<IActionResult> MonthlyReimbursement([FromQuery] MonthlyReimbursementQuery query, CancellationToken cancellationToken)
    {
        var validation = await _monthlyReimbursementValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            var fields = validation.Errors.Select(e => e.PropertyName).Distinct().ToList();
            return BadRequest(new ErrorResponse(new ErrorDetail(
                "VALIDATION_ERROR",
                "One or more fields are invalid.",
                fields,
                HttpContext.TraceIdentifier)));
        }

        var records = await _reportService.GetMonthlyReimbursementAsync(query.Year!.Value, query.Month!.Value, cancellationToken);
        return Ok(new MonthlyReimbursementReportResponse(records));
    }
}
