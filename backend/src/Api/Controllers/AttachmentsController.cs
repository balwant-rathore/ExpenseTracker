using Api.Authentication;
using Api.Authorization;
using Application.Attachments;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Shared.ErrorHandling;

namespace Api.Controllers;

[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly IValidator<UploadAttachmentRequest> _validator;

    public AttachmentsController(IAttachmentService attachmentService, IValidator<UploadAttachmentRequest> validator)
    {
        _attachmentService = attachmentService;
        _validator = validator;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        var request = new UploadAttachmentRequest
        {
            FileName = file?.FileName ?? string.Empty,
            ContentType = file?.ContentType ?? string.Empty,
            FileSize = file?.Length ?? 0,
            Content = file is null ? Stream.Null : file.OpenReadStream(),
            UploadedByEmployeeId = User.GetEmployeeId(),
        };

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var attachmentId = await _attachmentService.UploadAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new AttachmentUploadResponse(attachmentId));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await _attachmentService.DownloadAsync(
            User.GetEmployeeId(), User.GetRole(), id, cancellationToken);

        if (!result.Succeeded)
        {
            return result.FailureReason == AttachmentDownloadFailureReason.AttachmentNotFound
                ? NotFound(new ErrorResponse(new ErrorDetail(
                    "RESOURCE_NOT_FOUND", "Attachment not found.", [], HttpContext.TraceIdentifier)))
                : StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse(new ErrorDetail(
                    "AUTHORIZATION_FAILED", "You do not have permission to perform this action.", [], HttpContext.TraceIdentifier)));
        }

        var contentDisposition = new ContentDispositionHeaderValue("inline");
        contentDisposition.SetHttpFileName(result.OriginalFileName);
        Response.Headers.ContentDisposition = contentDisposition.ToString();
        return File(result.Content!, result.ContentType!);
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
}
