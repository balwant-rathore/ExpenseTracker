namespace Shared.ErrorHandling;

public record ErrorResponse(ErrorDetail Error);

public record ErrorDetail(string Code, string Message, IReadOnlyList<string> Fields, string TraceId);
