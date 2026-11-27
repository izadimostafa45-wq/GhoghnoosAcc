using System.ComponentModel.DataAnnotations;

namespace GhoghnoosAcc.Models;

public sealed class CreatePartyRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = "";

    [StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(300)]
    public string? Address { get; init; }
}
