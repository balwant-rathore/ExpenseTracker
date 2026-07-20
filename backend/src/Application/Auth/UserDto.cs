namespace Application.Auth;

public record UserDto(Guid Id, string Email, string EmployeeNumber, string FirstName, string LastName, string Role);
