using System.ComponentModel.DataAnnotations;

namespace GhoghnoosAcc.Models;

public sealed class CreateAccountRequest
{
    [Required, StringLength(30)]
    public string Code { get; init; } = "";

    [Required, StringLength(200)]
    public string Title { get; init; } = "";

    [StringLength(30)]
    public string Type { get; init; } = "General";
}
