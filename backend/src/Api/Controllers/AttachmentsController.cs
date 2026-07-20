using Api.Authorization;
using Application.Attachments;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.ErrorHandling;

namespace Api.Controllers;

[ApiController]
[Route("api/attachments")]
[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]
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
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        var request = new UploadAttachmentRequest
        {
            FileName = file?.FileName ?? string.Empty,
            ContentType = file?.ContentType ?? string.Empty,
            FileSize = file?.Length ?? 0,
            Content = file is null ? Stream.Null : file.OpenReadStream(),
        };

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var attachmentId = await _attachmentService.UploadAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new AttachmentUploadResponse(attachmentId));
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
