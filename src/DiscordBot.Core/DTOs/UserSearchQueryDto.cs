namespace DiscordBot.Core.DTOs;

/// <summary>
/// Query parameters for searching and filtering users.
/// </summary>
public class UserSearchQueryDto
{
    public string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDiscordLinked { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}
