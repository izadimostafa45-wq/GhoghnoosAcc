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
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization(options =>
{
    // Accounting data must stay inaccessible until the authenticated user has a
    // valid server-issued tenant claim. Membership validation and claim issuance
    // are implemented as a separate provisioning step.
    options.AddPolicy("TenantRequired", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => TenantRoleAuthorization.HasValidTenantAndRole(context.User)));
    options.AddPolicy("TenantRead", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => TenantRoleAuthorization.HasAnyRole(
            context.User, TenantRoleAuthorization.ReadRoles)));
    options.AddPolicy("TenantWrite", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => TenantRoleAuthorization.HasAnyRole(
            context.User, TenantRoleAuthorization.WriteRoles)));
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

// Optional one-time bootstrap for the first local administrator.
// Supply BootstrapAdmin:Email and BootstrapAdmin:Password through user-secrets or environment
// variables. Never commit credentials. Bootstrap runs only when PostgreSQL is configured
// and the Identity user table is empty; it never resets or overwrites an existing account.
var bootstrapEmail = builder.Configuration["BootstrapAdmin:Email"]?.Trim();
var bootstrapPassword = builder.Configuration["BootstrapAdmin:Password"];
if (!string.IsNullOrWhiteSpace(connectionString)
    && !string.IsNullOrWhiteSpace(bootstrapEmail)
    && !string.IsNullOrWhiteSpace(bootstrapPassword))
{
    using var bootstrapScope = app.Services.CreateScope();
    var bootstrapUserManager = bootstrapScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (!await bootstrapUserManager.Users.AnyAsync())
    {
        var bootstrapUser = new ApplicationUser
        {
            UserName = bootstrapEmail,
            Email = bootstrapEmail,
            EmailConfirmed = true
        };
        var createResult = await bootstrapUserManager.CreateAsync(bootstrapUser, bootstrapPassword);
        if (!createResult.Succeeded)
        {
            var errorCodes = string.Join(", ", createResult.Errors.Select(error => error.Code));
            throw new InvalidOperationException(
                $"Initial administrator could not be created. Identity validation errors: {errorCodes}");
        }

        app.Logger.LogWarning(
            "Initial administrator account was created from BootstrapAdmin configuration. Remove the bootstrap password setting after successful sign-in.");
    }
}

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

var accountingApi = app.MapGroup("/api/accounting")
    .RequireAuthorization("TenantRequired")
    .RequireAuthorization("TenantRead");

accountingApi.MapGet("/accounts", async Task<IResult> (IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var db = services.GetRequiredService<AppDbContext>();
    var accounts = await db.Accounts.AsNoTracking().ForTenant(tenantId)
        .OrderBy(x => x.Code)
        .Select(x => new { x.Id, x.Code, x.Title, x.Type, x.IsActive })
        .ToListAsync();
    return Results.Ok(accounts);
});

accountingApi.MapPost("/accounts", async Task<IResult> (CreateAccountRequest request, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);
    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Title))
        return Results.BadRequest(new { message = "Account code and title are required." });

    var db = services.GetRequiredService<AppDbContext>();
    var code = request.Code.Trim();
    var title = request.Title.Trim();
    if (await db.Accounts.ForTenant(tenantId).AnyAsync(x => x.Code == code))
        return Results.Conflict(new { message = "An account with this code already exists." });

    var account = new Account
    {
        TenantId = tenantId,
        Code = code,
        Title = title,
        Type = string.IsNullOrWhiteSpace(request.Type) ? "General" : request.Type.Trim(),
        IsActive = true
    };
    db.Accounts.Add(account);
    await db.SaveChangesAsync();
    return Results.Created($"/api/accounting/accounts/{account.Id}",
        new { account.Id, account.Code, account.Title, account.Type, account.IsActive });
}).RequireAuthorization("TenantWrite");

accountingApi.MapGet("/entries", async Task<IResult> (IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var db = services.GetRequiredService<AppDbContext>();
    var entries = await db.JournalEntries.AsNoTracking().ForTenant(tenantId)
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
    IServiceProvider services,
    ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var validation = validator.Validate(request);
    if (!validation.IsValid)
        return Results.BadRequest(validation);

    var db = services.GetRequiredService<AppDbContext>();
    var names = request.Lines!.Select(x => x!.AccountName!.Trim()).Distinct().ToList();
    var accounts = await db.Accounts
        .ForTenant(tenantId)
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
        TenantId = tenantId,
        DocumentNumber = $"JV-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
        // The document date is entered as a calendar date, but Npgsql maps DateTime to timestamptz by default.
        // Mark the submitted date as UTC so it can be stored safely in the existing schema.
        DocumentDate = DateTime.SpecifyKind(request.EntryDate.Date, DateTimeKind.Utc),
        Description = request.Description?.Trim(),
        IsPosted = false,
        Lines = request.Lines!.Select(line => new JournalEntryLine
        {
            TenantId = tenantId,
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
}).RequireAuthorization("TenantWrite");

accountingApi.MapGet("/entries/{id:int}", async Task<IResult> (int id, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var db = services.GetRequiredService<AppDbContext>();
    var entry = await db.JournalEntries.AsNoTracking()
        .ForTenant(tenantId)
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


accountingApi.MapGet("/parties", async Task<IResult> (IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var db = services.GetRequiredService<AppDbContext>();
    var parties = await db.Parties.AsNoTracking().ForTenant(tenantId)
        .OrderBy(x => x.Name)
        .Select(x => new { x.Id, x.Name, x.Phone, x.Address, x.IsActive })
        .Take(1000)
        .ToListAsync();
    return Results.Ok(parties);
});

accountingApi.MapPost("/parties", async Task<IResult> (CreatePartyRequest request, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name))
        return Results.BadRequest(new { message = "نام شخص یا شرکت الزامی است." });
    if (name.Length > 200 || (request.Phone?.Length ?? 0) > 30 || (request.Address?.Length ?? 0) > 300)
        return Results.BadRequest(new { message = "طول یکی از فیلدها بیش از حد مجاز است." });

    var db = services.GetRequiredService<AppDbContext>();
    var party = new Party
    {
        TenantId = tenantId,
        Name = name,
        Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
        Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
    db.Parties.Add(party);
    await db.SaveChangesAsync();
    return Results.Created($"/api/accounting/parties/{party.Id}",
        new { party.Id, party.Name, party.Phone, party.Address, party.IsActive });
}).RequireAuthorization("TenantWrite");


accountingApi.MapGet("/reports/trial-balance", async Task<IResult> (
    DateTime? from,
    DateTime? to,
    IServiceProvider services,
    ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);
    if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
        return Results.BadRequest(new { message = "تاریخ شروع نباید بعد از تاریخ پایان باشد." });

    var start = from.HasValue
        ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc)
        : DateTime.SpecifyKind(new DateTime(1900, 1, 1), DateTimeKind.Utc);
    var endExclusive = to.HasValue
        ? DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc)
        : DateTime.SpecifyKind(DateTime.MaxValue.Date, DateTimeKind.Utc);

    var db = services.GetRequiredService<AppDbContext>();
    var balances = await db.JournalEntryLines.AsNoTracking()
        .ForTenant(tenantId)
        .Where(line => line.JournalEntry.TenantId == tenantId
            && line.JournalEntry.DocumentDate >= start
            && line.JournalEntry.DocumentDate < endExclusive)
        .GroupBy(line => new { line.AccountId, line.Account.Code, line.Account.Title })
        .Select(group => new
        {
            accountId = group.Key.AccountId,
            code = group.Key.Code,
            title = group.Key.Title,
            debit = group.Sum(line => line.Debit),
            credit = group.Sum(line => line.Credit),
            documentLines = group.Count()
        })
        .OrderBy(row => row.code)
        .ToListAsync();

    return Results.Ok(new
    {
        from = from?.Date,
        to = to?.Date,
        accounts = balances,
        debitTotal = balances.Sum(row => row.debit),
        creditTotal = balances.Sum(row => row.credit),
        difference = balances.Sum(row => row.debit) - balances.Sum(row => row.credit)
    });
});

app.Run();
