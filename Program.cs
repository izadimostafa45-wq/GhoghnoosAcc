using GhoghnoosAcc.Data;
using GhoghnoosAcc.Models;
using GhoghnoosAcc.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<JournalEntryValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

// Database schema changes are managed exclusively through reviewed EF Core migrations.
 // Do not call EnsureCreatedAsync here: it bypasses migration history and cannot safely
 // evolve an existing PostgreSQL database when Identity and tenant tables are introduced.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.MapGet("/api/accounting/status", () => Results.Ok(new
{
    application = "GhoghnoosAcc",
    databaseConfigured = !string.IsNullOrWhiteSpace(connectionString),
    persistence = string.IsNullOrWhiteSpace(connectionString) ? "NotConfigured" : "PostgreSQL"
}));

app.MapPost("/api/accounting/validate-entry",
    (JournalEntryValidationRequest request, JournalEntryValidator validator) =>
    {
        var result = validator.Validate(request);
        return result.IsValid ? (IResult)Results.Ok(result) : Results.BadRequest(result);
    });

var accountingApi = app.MapGroup("/api/accounting").RequireAuthorization();

accountingApi.MapGet("/accounts", async Task<IResult> (IServiceProvider services) =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var db = services.GetRequiredService<AppDbContext>();
    var accounts = await db.Accounts.AsNoTracking()
        .OrderBy(x => x.Code)
        .Select(x => new { x.Id, x.Code, x.Title, x.Type, x.IsActive })
        .ToListAsync();
    return Results.Ok(accounts);
});

accountingApi.MapPost("/accounts", async Task<IResult> (CreateAccountRequest request, IServiceProvider services) =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);
    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Title))
        return Results.BadRequest(new { message = "Account code and title are required." });

    var db = services.GetRequiredService<AppDbContext>();
    var code = request.Code.Trim();
    var title = request.Title.Trim();
    if (await db.Accounts.AnyAsync(x => x.Code == code))
        return Results.Conflict(new { message = "An account with this code already exists." });

    var account = new Account
    {
        Code = code,
        Title = title,
        Type = string.IsNullOrWhiteSpace(request.Type) ? "General" : request.Type.Trim(),
        IsActive = true
    };
    db.Accounts.Add(account);
    await db.SaveChangesAsync();
    return Results.Created($"/api/accounting/accounts/{account.Id}",
        new { account.Id, account.Code, account.Title, account.Type, account.IsActive });
});

accountingApi.MapGet("/entries", async Task<IResult> (IServiceProvider services) =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var db = services.GetRequiredService<AppDbContext>();
    var entries = await db.JournalEntries.AsNoTracking()
        .OrderByDescending(x => x.DocumentDate)
        .ThenByDescending(x => x.Id)
        .Select(x => new
        {
            x.Id, x.DocumentNumber, x.DocumentDate, x.Description, x.IsPosted,
            DebitTotal = x.Lines.Sum(line => (decimal?)line.Debit) ?? 0m,
            CreditTotal = x.Lines.Sum(line => (decimal?)line.Credit) ?? 0m
        })
        .Take(200)
        .ToListAsync();
    return Results.Ok(entries);
});

accountingApi.MapPost("/entries", async Task<IResult> (
    JournalEntryValidationRequest request,
    JournalEntryValidator validator,
    IServiceProvider services) =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var validation = validator.Validate(request);
    if (!validation.IsValid)
        return Results.BadRequest(validation);

    var db = services.GetRequiredService<AppDbContext>();
    var names = request.Lines!.Select(x => x!.AccountName!.Trim()).Distinct().ToList();
    var accounts = await db.Accounts
        .Where(x => x.IsActive && (names.Contains(x.Code) || names.Contains(x.Title)))
        .ToListAsync();

    var accountMap = accounts
        .SelectMany(a => new[] { (Key: a.Code, Account: a), (Key: a.Title, Account: a) })
        .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().Account, StringComparer.OrdinalIgnoreCase);

    var missing = names.Where(name => !accountMap.ContainsKey(name)).ToList();
    if (missing.Count > 0)
        return Results.BadRequest(new
        {
            message = "Create these accounts first, then submit the journal entry.",
            missingAccounts = missing
        });

    var entry = new JournalEntry
    {
        DocumentNumber = $"JV-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
        // The document date is entered as a calendar date, but Npgsql maps DateTime to timestamptz by default.
        // Mark the submitted date as UTC so it can be stored safely in the existing schema.
        DocumentDate = DateTime.SpecifyKind(request.EntryDate.Date, DateTimeKind.Utc),
        Description = request.Description?.Trim(),
        IsPosted = false,
        Lines = request.Lines!.Select(line => new JournalEntryLine
        {
            AccountId = accountMap[line!.AccountName!.Trim()].Id,
            Debit = line.Debit,
            Credit = line.Credit
        }).ToList()
    };

    await using var transaction = await db.Database.BeginTransactionAsync();
    db.JournalEntries.Add(entry);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Created($"/api/accounting/entries/{entry.Id}", new
    {
        entry.Id, entry.DocumentNumber, entry.DocumentDate, entry.Description,
        entry.IsPosted, validation.DebitTotal, validation.CreditTotal
    });
});

accountingApi.MapGet("/entries/{id:int}", async Task<IResult> (int id, IServiceProvider services) =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var db = services.GetRequiredService<AppDbContext>();
    var entry = await db.JournalEntries.AsNoTracking()
        .Include(x => x.Lines)
        .ThenInclude(x => x.Account)
        .Where(x => x.Id == id)
        .Select(x => new
        {
            x.Id, x.DocumentNumber, x.DocumentDate, x.Description, x.IsPosted,
            Lines = x.Lines.Select(line => new
            {
                line.Id, line.AccountId, AccountCode = line.Account.Code,
                AccountTitle = line.Account.Title, line.Debit, line.Credit, line.Description
            })
        })
        .FirstOrDefaultAsync();

    return entry is null ? Results.NotFound() : Results.Ok(entry);
});

app.Run();
