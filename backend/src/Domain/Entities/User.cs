namespace Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Employee Employee { get; set; } = null!;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordResetOtp> PasswordResetOtps { get; set; } = new List<PasswordResetOtp>();
}
