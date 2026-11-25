using GhoghnoosAcc.Models;
using Microsoft.EntityFrameworkCore;

namespace GhoghnoosAcc.Services;

/// <summary>
/// Applies the selected tenant filter consistently to tenant-owned queries.
/// Use this for reads and existence checks; never accept tenant IDs from request data.
/// </summary>
public static class TenantQueryExtensions
{
    public static IQueryable<T> ForTenant<T>(this IQueryable<T> query, Guid tenantId)
        where T : class, ITenantOwned =>
        query.Where(entity => entity.TenantId == tenantId);
}
