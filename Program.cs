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

accountingApi.MapPut("/accounts/{id:int}", async Task<IResult> (int id, UpdateAccountRequest request, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var code = request.Code?.Trim();
    var title = request.Title?.Trim();
    var type = request.Type?.Trim();
    if (string.IsNullOrWhiteSpace(code) || code.Length > 30 || string.IsNullOrWhiteSpace(title) || title.Length > 200 || string.IsNullOrWhiteSpace(type) || type.Length > 30)
        return Results.BadRequest(new { message = "کد، عنوان و نوع حساب را به‌درستی وارد کنید." });
    var db = services.GetRequiredService<AppDbContext>();
    var account = await db.Accounts.ForTenant(tenantId).FirstOrDefaultAsync(x => x.Id == id);
    if (account is null) return Results.NotFound(new { message = "حساب پیدا نشد." });
    if (await db.Accounts.ForTenant(tenantId).AnyAsync(x => x.Id != id && x.Code == code))
        return Results.Conflict(new { message = "این کد حساب قبلاً استفاده شده است." });
    var hasPostedLines = await db.JournalEntryLines.ForTenant(tenantId)
        .AnyAsync(line => line.AccountId == id && line.JournalEntry.IsPosted);
    if (hasPostedLines && (account.Code != code || account.Title != title || account.Type != type))
        return Results.Conflict(new { message = "حساب در سند ثبت‌نهایی‌شده استفاده شده؛ کد، عنوان و نوع آن قابل تغییر نیست. فقط می‌توانید آن را غیرفعال کنید." });
    account.Code = code; account.Title = title; account.Type = type;
    await db.SaveChangesAsync();
    return Results.Ok(new { account.Id, account.Code, account.Title, account.Type, account.IsActive });
}).RequireAuthorization("TenantWrite");

accountingApi.MapPost("/accounts/{id:int}/toggle-active", async Task<IResult> (int id, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var db = services.GetRequiredService<AppDbContext>();
    var account = await db.Accounts.ForTenant(tenantId).FirstOrDefaultAsync(x => x.Id == id);
    if (account is null) return Results.NotFound(new { message = "حساب پیدا نشد." });
    account.IsActive = !account.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(new { account.Id, account.IsActive, message = account.IsActive ? "حساب فعال شد." : "حساب غیرفعال شد." });
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

    var partyNames = request.Lines!
        .Where(line => !string.IsNullOrWhiteSpace(line!.PartyName))
        .Select(line => line!.PartyName!.Trim())
        .Distinct()
        .ToList();
    var parties = partyNames.Count == 0
        ? new List<Party>()
        : await db.Parties.ForTenant(tenantId)
            .Where(x => x.IsActive && partyNames.Contains(x.Name))
            .ToListAsync();
    var partyMap = parties.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
    var missingParties = partyNames.Where(name => !partyMap.ContainsKey(name)).ToList();
    if (missingParties.Count > 0)
        return Results.BadRequest(new { message = "شخص انتخاب‌شده وجود ندارد یا غیرفعال است.", missingParties });

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
            PartyId = string.IsNullOrWhiteSpace(line.PartyName) ? null : partyMap[line.PartyName.Trim()].Id,
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

accountingApi.MapDelete("/entries/{id:int}", async Task<IResult> (int id, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var db = services.GetRequiredService<AppDbContext>();
    var entry = await db.JournalEntries.ForTenant(tenantId)
        .Include(x => x.Lines)
        .FirstOrDefaultAsync(x => x.Id == id);
    if (entry is null) return Results.NotFound(new { message = "سند پیدا نشد." });
    if (entry.IsPosted) return Results.Conflict(new { message = "سند ثبت‌نهایی‌شده قابل حذف نیست." });
    db.JournalEntries.Remove(entry);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "پیش‌نویس سند حذف شد.", entry.Id, entry.DocumentNumber });
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
                AccountTitle = line.Account.Title, PartyName = line.Party == null ? null : line.Party.Name,
                line.PartyId, line.Debit, line.Credit, line.Description
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


accountingApi.MapPut("/parties/{id:int}", async Task<IResult> (int id, UpdatePartyRequest request, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || (request.Phone?.Length ?? 0) > 30 || (request.Address?.Length ?? 0) > 300)
        return Results.BadRequest(new { message = "نام الزامی است و طول فیلدها باید در محدوده مجاز باشد." });
    var db = services.GetRequiredService<AppDbContext>();
    var party = await db.Parties.ForTenant(tenantId).FirstOrDefaultAsync(x => x.Id == id);
    if (party is null) return Results.NotFound(new { message = "شخص پیدا نشد." });
    party.Name = name;
    party.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
    party.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(new { party.Id, party.Name, party.Phone, party.Address, party.IsActive });
}).RequireAuthorization("TenantWrite");

accountingApi.MapPost("/parties/{id:int}/toggle-active", async Task<IResult> (int id, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var db = services.GetRequiredService<AppDbContext>();
    var party = await db.Parties.ForTenant(tenantId).FirstOrDefaultAsync(x => x.Id == id);
    if (party is null) return Results.NotFound(new { message = "شخص پیدا نشد." });
    party.IsActive = !party.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(new { party.Id, party.IsActive, message = party.IsActive ? "شخص فعال شد." : "شخص غیرفعال شد." });
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
            && line.JournalEntry.IsPosted
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


accountingApi.MapGet("/reports/balance-sheet", async Task<IResult> (
    DateTime? asOf,
    IServiceProvider services,
    ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var cutoffExclusive = asOf.HasValue
        ? DateTime.SpecifyKind(asOf.Value.Date.AddDays(1), DateTimeKind.Utc)
        : DateTime.SpecifyKind(DateTime.MaxValue.Date, DateTimeKind.Utc);
    var db = services.GetRequiredService<AppDbContext>();
    var balances = await db.JournalEntryLines.AsNoTracking()
        .ForTenant(tenantId)
        .Where(line => line.TenantId == tenantId && line.JournalEntry.TenantId == tenantId
            && line.JournalEntry.IsPosted && line.JournalEntry.DocumentDate < cutoffExclusive
            && (line.Account.Type == "Asset" || line.Account.Type == "Liability" || line.Account.Type == "Equity"))
        .GroupBy(line => new { line.AccountId, line.Account.Code, line.Account.Title, line.Account.Type })
        .Select(group => new {
            accountId = group.Key.AccountId, code = group.Key.Code, title = group.Key.Title, type = group.Key.Type,
            debit = group.Sum(line => line.Debit), credit = group.Sum(line => line.Credit)
        })
        .OrderBy(row => row.type).ThenBy(row => row.code)
        .ToListAsync();
    var assets = balances.Where(x => x.type == "Asset").Sum(x => x.debit - x.credit);
    var liabilities = balances.Where(x => x.type == "Liability").Sum(x => x.credit - x.debit);
    var equity = balances.Where(x => x.type == "Equity").Sum(x => x.credit - x.debit);
    return Results.Ok(new {
        asOf = asOf?.Date, assetAccounts = balances.Where(x => x.type == "Asset"),
        liabilityAccounts = balances.Where(x => x.type == "Liability"),
        equityAccounts = balances.Where(x => x.type == "Equity"),
        assetsTotal = assets, liabilitiesTotal = liabilities, equityTotal = equity,
        liabilitiesAndEquityTotal = liabilities + equity,
        difference = assets - liabilities - equity
    });
});

accountingApi.MapGet("/reports/income-statement", async Task<IResult> (
    DateTime? from,
    DateTime? to,
    IServiceProvider services,
    ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
        return Results.BadRequest(new { message = "تاریخ شروع نباید بعد از تاریخ پایان باشد." });

    var start = from.HasValue ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc)
        : DateTime.SpecifyKind(new DateTime(1900, 1, 1), DateTimeKind.Utc);
    var endExclusive = to.HasValue ? DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc)
        : DateTime.SpecifyKind(DateTime.MaxValue.Date, DateTimeKind.Utc);

    var db = services.GetRequiredService<AppDbContext>();
    var balances = await db.JournalEntryLines.AsNoTracking()
        .ForTenant(tenantId)
        .Where(line => line.TenantId == tenantId && line.JournalEntry.TenantId == tenantId
            && line.JournalEntry.IsPosted
            && line.JournalEntry.DocumentDate >= start && line.JournalEntry.DocumentDate < endExclusive
            && (line.Account.Type == "Revenue" || line.Account.Type == "Expense"))
        .GroupBy(line => new { line.AccountId, line.Account.Code, line.Account.Title, line.Account.Type })
        .Select(group => new {
            accountId = group.Key.AccountId, code = group.Key.Code, title = group.Key.Title, type = group.Key.Type,
            debit = group.Sum(line => line.Debit), credit = group.Sum(line => line.Credit)
        })
        .OrderBy(row => row.type).ThenBy(row => row.code)
        .ToListAsync();

    var revenue = balances.Where(x => x.type == "Revenue").Sum(x => x.credit - x.debit);
    var expenses = balances.Where(x => x.type == "Expense").Sum(x => x.debit - x.credit);
    return Results.Ok(new {
        from = from?.Date, to = to?.Date,
        revenueAccounts = balances.Where(x => x.type == "Revenue"),
        expenseAccounts = balances.Where(x => x.type == "Expense"),
        revenueTotal = revenue, expenseTotal = expenses, netProfit = revenue - expenses
    });
});

accountingApi.MapGet("/reports/general-ledger", async Task<IResult> (
    int? accountId,
    DateTime? from,
    DateTime? to,
    IServiceProvider services,
    ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);
    if (accountId is null)
        return Results.BadRequest(new { message = "انتخاب حساب الزامی است." });
    if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
        return Results.BadRequest(new { message = "تاریخ شروع نباید بعد از تاریخ پایان باشد." });

    var start = from.HasValue
        ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc)
        : DateTime.SpecifyKind(new DateTime(1900, 1, 1), DateTimeKind.Utc);
    var endExclusive = to.HasValue
        ? DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc)
        : DateTime.SpecifyKind(DateTime.MaxValue.Date, DateTimeKind.Utc);

    var db = services.GetRequiredService<AppDbContext>();
    var account = await db.Accounts.AsNoTracking().ForTenant(tenantId)
        .Where(x => x.Id == accountId.Value)
        .Select(x => new { x.Id, x.Code, x.Title })
        .FirstOrDefaultAsync();
    if (account is null)
        return Results.NotFound(new { message = "حساب انتخاب‌شده پیدا نشد." });

    var openingBalance = from.HasValue
        ? await db.JournalEntryLines.AsNoTracking()
            .ForTenant(tenantId)
            .Where(line => line.TenantId == tenantId
                && line.AccountId == account.Id
                && line.JournalEntry.TenantId == tenantId
                && line.JournalEntry.IsPosted
                && line.JournalEntry.DocumentDate < start)
            .SumAsync(line => (decimal?)(line.Debit - line.Credit)) ?? 0m
        : 0m;

    var lines = await db.JournalEntryLines.AsNoTracking()
        .ForTenant(tenantId)
        .Where(line => line.TenantId == tenantId
            && line.AccountId == account.Id
            && line.JournalEntry.TenantId == tenantId
            && line.JournalEntry.IsPosted
            && line.JournalEntry.DocumentDate >= start
            && line.JournalEntry.DocumentDate < endExclusive)
        .OrderBy(line => line.JournalEntry.DocumentDate)
        .ThenBy(line => line.JournalEntry.Id)
        .ThenBy(line => line.Id)
        .Select(line => new
        {
            line.Id,
            line.JournalEntry.DocumentNumber,
            line.JournalEntry.DocumentDate,
            DocumentDescription = line.JournalEntry.Description,
            line.Debit,
            line.Credit,
            LineDescription = line.Description
        })
        .ToListAsync();

    decimal runningBalance = openingBalance;
    var ledgerLines = lines.Select(line =>
    {
        runningBalance += line.Debit - line.Credit;
        return new
        {
            line.Id,
            line.DocumentNumber,
            line.DocumentDate,
            documentDescription = line.DocumentDescription,
            lineDescription = line.LineDescription,
            line.Debit,
            line.Credit,
            balance = runningBalance
        };
    }).ToList();

    return Results.Ok(new
    {
        account,
        from = from?.Date,
        to = to?.Date,
        openingBalance,
        lines = ledgerLines,
        debitTotal = lines.Sum(x => x.Debit),
        creditTotal = lines.Sum(x => x.Credit),
        endingBalance = runningBalance
    });
});


accountingApi.MapPost("/entries/{id:int}/post", async Task<IResult> (
    int id,
    IServiceProvider services,
    ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId)
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("PostgreSQL is not configured. Set ConnectionStrings:DefaultConnection.", statusCode: 503);

    var db = services.GetRequiredService<AppDbContext>();
    var entry = await db.JournalEntries
        .ForTenant(tenantId)
        .Include(x => x.Lines)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (entry is null)
        return Results.NotFound(new { message = "سند پیدا نشد." });
    if (entry.IsPosted)
        return Results.Conflict(new { message = "این سند قبلاً ثبت نهایی شده است." });
    if (entry.Lines.Count < 2)
        return Results.BadRequest(new { message = "سند باید حداقل دو ردیف داشته باشد." });

    var debitTotal = entry.Lines.Sum(line => line.Debit);
    var creditTotal = entry.Lines.Sum(line => line.Credit);
    if (entry.Lines.Any(line => line.Debit < 0 || line.Credit < 0
            || (line.Debit > 0 && line.Credit > 0)
            || (line.Debit == 0 && line.Credit == 0)))
        return Results.BadRequest(new { message = "هر ردیف باید مبلغ مثبت را فقط در یکی از ستون‌های بدهکار یا بستانکار داشته باشد." });
    if (debitTotal <= 0 || Math.Abs(debitTotal - creditTotal) > 0.005m)
        return Results.BadRequest(new
        {
            message = "سند متوازن نیست و امکان ثبت نهایی ندارد.",
            debitTotal,
            creditTotal,
            difference = debitTotal - creditTotal
        });

    entry.IsPosted = true;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "سند با موفقیت ثبت نهایی شد.",
        entry.Id,
        entry.DocumentNumber,
        entry.IsPosted,
        debitTotal,
        creditTotal
    });
}).RequireAuthorization("TenantWrite");


accountingApi.MapGet("/invoices", async Task<IResult> (IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var db = services.GetRequiredService<AppDbContext>();
    var rows = await db.Invoices.AsNoTracking().ForTenant(tenantId)
        .OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.Id)
        .Select(x => new { x.Id, x.Number, x.Kind, x.Status, x.InvoiceDate, x.PartyId, PartyName = x.Party.Name, x.Subtotal, x.TaxTotal, x.Total, x.JournalEntryId })
        .ToListAsync();
    var invoiceIds = rows.Select(x => x.Id).ToList();
    var paidByInvoice = await db.PaymentTransactions.AsNoTracking().ForTenant(tenantId)
        .Where(x => x.InvoiceId.HasValue && invoiceIds.Contains(x.InvoiceId.Value))
        .GroupBy(x => x.InvoiceId!.Value)
        .Select(g => new { InvoiceId = g.Key, AmountPaid = g.Sum(x => x.Amount) })
        .ToDictionaryAsync(x => x.InvoiceId, x => x.AmountPaid);
    return Results.Ok(rows.Select(x =>
    {
        var amountPaid = paidByInvoice.GetValueOrDefault(x.Id, 0m);
        return new { x.Id, x.Number, x.Kind, x.Status, x.InvoiceDate, x.PartyId, x.PartyName,
            x.Subtotal, x.TaxTotal, x.Total, x.JournalEntryId, AmountPaid = amountPaid,
            Remaining = Math.Max(0m, x.Total - amountPaid) };
    }));
});

accountingApi.MapPost("/invoices", async Task<IResult> (InvoiceCreateRequest request, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var number = request.Number?.Trim();
    if (string.IsNullOrWhiteSpace(number) || number.Length > 40 || request.InvoiceDate == default ||
        (request.Kind != InvoiceKinds.Sales && request.Kind != InvoiceKinds.Purchase) ||
        request.PartyId <= 0 || request.Lines is null || request.Lines.Count == 0)
        return Results.BadRequest(new { message = "نوع، شماره، تاریخ، طرف حساب و حداقل یک ردیف معتبر الزامی است." });
    if (request.Lines.Any(x => string.IsNullOrWhiteSpace(x.Description) || x.Description.Trim().Length > 200 ||
        x.Quantity <= 0 || x.UnitPrice < 0 || x.TaxRate < 0 || x.TaxRate > 100 ||
        decimal.Round(x.Quantity * x.UnitPrice, 2) < 0))
        return Results.BadRequest(new { message = "اطلاعات ردیف فاکتور معتبر نیست." });
    var db = services.GetRequiredService<AppDbContext>();
    if (!await db.Parties.ForTenant(tenantId).AnyAsync(x => x.Id == request.PartyId && x.IsActive))
        return Results.BadRequest(new { message = "طرف حساب فعال پیدا نشد." });
    if (await db.Invoices.ForTenant(tenantId).AnyAsync(x => x.Number == number))
        return Results.Conflict(new { message = "شماره فاکتور قبلاً استفاده شده است." });
    var lines = request.Lines.Select(x =>
    {
        var subtotal = decimal.Round(x.Quantity * x.UnitPrice, 2);
        var tax = decimal.Round(subtotal * x.TaxRate / 100m, 2);
        return new InvoiceLine { TenantId = tenantId, Description = x.Description.Trim(), Quantity = x.Quantity, UnitPrice = x.UnitPrice, TaxRate = x.TaxRate, LineSubtotal = subtotal, LineTax = tax, LineTotal = subtotal + tax };
    }).ToList();
    var invoice = new Invoice { TenantId = tenantId, Number = number, Kind = request.Kind, Status = InvoiceStatuses.Draft,
        InvoiceDate = DateTime.SpecifyKind(request.InvoiceDate.Date, DateTimeKind.Utc), PartyId = request.PartyId,
        Notes = request.Notes?.Trim(), Lines = lines, Subtotal = lines.Sum(x => x.LineSubtotal), TaxTotal = lines.Sum(x => x.LineTax),
        Total = lines.Sum(x => x.LineTotal) };
    db.Invoices.Add(invoice);
    await db.SaveChangesAsync();
    return Results.Created($"/api/accounting/invoices/{invoice.Id}", new { invoice.Id, invoice.Number, invoice.Kind, invoice.Status, invoice.Subtotal, invoice.TaxTotal, invoice.Total });
}).RequireAuthorization("TenantWrite");

accountingApi.MapPost("/invoices/{id:int}/post", async Task<IResult> (int id, InvoicePostRequest request, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var db = services.GetRequiredService<AppDbContext>();
    var invoice = await db.Invoices.ForTenant(tenantId).Include(x => x.Lines).Include(x => x.Party).FirstOrDefaultAsync(x => x.Id == id);
    if (invoice is null) return Results.NotFound(new { message = "فاکتور پیدا نشد." });
    if (invoice.Status != InvoiceStatuses.Draft) return Results.Conflict(new { message = "فاکتور قبلاً ثبت نهایی شده است." });
    var accounts = await db.Accounts.ForTenant(tenantId).Where(x => x.IsActive).ToListAsync();
    var counterpartyType = invoice.Kind == InvoiceKinds.Sales ? "Asset" : "Liability";
    var operatingType = invoice.Kind == InvoiceKinds.Sales ? "Revenue" : "Expense";
    var taxType = invoice.Kind == InvoiceKinds.Sales ? "Liability" : "Asset";
    var counterparty = accounts.FirstOrDefault(a => a.Id == request.CounterpartyAccountId && a.Type == counterpartyType);
    var operating = accounts.FirstOrDefault(a => a.Id == request.OperatingAccountId && a.Type == operatingType);
    var taxAccount = request.TaxAccountId.HasValue
        ? accounts.FirstOrDefault(a => a.Id == request.TaxAccountId.Value && a.Type == taxType)
        : null;
    if (counterparty is null || operating is null ||
        (invoice.TaxTotal > 0 && taxAccount is null) ||
        (invoice.TaxTotal == 0 && request.TaxAccountId.HasValue && taxAccount is null))
        return Results.BadRequest(new { message = "حساب‌های انتخاب‌شده معتبر نیستند یا با نوع فاکتور سازگار نیستند. حساب دریافتنی/پرداختنی، درآمد/هزینه و در صورت نیاز مالیات را بررسی کنید." });
    var docNo = "INV-" + invoice.Number;
    if (await db.JournalEntries.ForTenant(tenantId).AnyAsync(x => x.DocumentNumber == docNo))
        return Results.Conflict(new { message = "شماره سند حسابداری مرتبط تکراری است؛ شماره فاکتور را بررسی کنید." });
    var journal = new JournalEntry { TenantId = tenantId, DocumentNumber = docNo,
        DocumentDate = DateTime.SpecifyKind(invoice.InvoiceDate.Date, DateTimeKind.Utc),
        Description = (invoice.Kind == InvoiceKinds.Sales ? "فاکتور فروش " : "فاکتور خرید ") + invoice.Number,
        IsPosted = true };
    if (invoice.Kind == InvoiceKinds.Sales)
    {
        journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = counterparty.Id, PartyId = invoice.PartyId, Debit = invoice.Total, Description = "مانده فاکتور" });
        journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = operating.Id, PartyId = invoice.PartyId, Credit = invoice.Subtotal, Description = "فروش" });
        if (invoice.TaxTotal > 0) journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = taxAccount!.Id, PartyId = invoice.PartyId, Credit = invoice.TaxTotal, Description = "مالیات فروش" });
    }
    else
    {
        journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = operating.Id, PartyId = invoice.PartyId, Debit = invoice.Subtotal, Description = "خرید" });
        if (invoice.TaxTotal > 0) journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = taxAccount!.Id, PartyId = invoice.PartyId, Debit = invoice.TaxTotal, Description = "مالیات خرید" });
        journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = counterparty.Id, PartyId = invoice.PartyId, Credit = invoice.Total, Description = "مانده فاکتور" });
    }
    invoice.CounterpartyAccountId = counterparty.Id;
    invoice.OperatingAccountId = operating.Id;
    invoice.TaxAccountId = invoice.TaxTotal > 0 ? taxAccount!.Id : request.TaxAccountId;
    invoice.Status = InvoiceStatuses.Posted;
    invoice.JournalEntry = journal;
    db.JournalEntries.Add(journal);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "فاکتور ثبت نهایی شد و سند حسابداری " + journal.DocumentNumber + " ایجاد شد.", invoice.Id, journalEntryId = journal.Id, journal.DocumentNumber });
}).RequireAuthorization("TenantWrite");

accountingApi.MapGet("/payments", async Task<IResult> (IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var db = services.GetRequiredService<AppDbContext>();
    var rows = await db.PaymentTransactions.AsNoTracking().ForTenant(tenantId)
        .OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.Id)
        .Select(x => new { x.Id, x.Number, x.Kind, x.PaymentDate, x.PartyId, PartyName = x.Party.Name,
            CashAccountTitle = x.CashAccount.Title, x.InvoiceId, InvoiceNumber = x.Invoice == null ? null : x.Invoice.Number,
            x.Amount, JournalEntryNumber = x.JournalEntry == null ? null : x.JournalEntry.DocumentNumber })
        .ToListAsync();
    return Results.Ok(rows);
});

accountingApi.MapPost("/payments", async Task<IResult> (PaymentCreateRequest request, IServiceProvider services, ITenantContext tenantContext) =>
{
    if (tenantContext.TenantId is not Guid tenantId) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    var number = request.Number?.Trim();
    if (string.IsNullOrWhiteSpace(number) || number.Length > 40 || request.PaymentDate == default ||
        (request.Kind != PaymentKinds.Receipt && request.Kind != PaymentKinds.Payment) ||
        request.PartyId <= 0 || request.CashAccountId <= 0 || request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
        return Results.BadRequest(new { message = "نوع، شماره، تاریخ، طرف حساب، حساب صندوق/بانک و مبلغ معتبر الزامی است." });
    var db = services.GetRequiredService<AppDbContext>();
    if (await db.PaymentTransactions.ForTenant(tenantId).AnyAsync(x => x.Number == number))
        return Results.Conflict(new { message = "شماره رسید/پرداخت تکراری است." });
    var party = await db.Parties.ForTenant(tenantId).FirstOrDefaultAsync(x => x.Id == request.PartyId && x.IsActive);
    var cash = await db.Accounts.ForTenant(tenantId).FirstOrDefaultAsync(x => x.Id == request.CashAccountId && x.IsActive && x.Type == "Asset");
    if (party is null || cash is null) return Results.BadRequest(new { message = "طرف حساب فعال یا حساب دارایی صندوق/بانک معتبر پیدا نشد." });
    var invoice = request.InvoiceId.HasValue ? await db.Invoices.ForTenant(tenantId).FirstOrDefaultAsync(x => x.Id == request.InvoiceId.Value && x.Status == InvoiceStatuses.Posted && x.PartyId == party.Id) : null;
    if (request.InvoiceId.HasValue && invoice is null) return Results.BadRequest(new { message = "فاکتور ثبت‌نهایی‌شده متعلق به این طرف حساب پیدا نشد." });
    if (invoice is null) return Results.BadRequest(new { message = "برای جلوگیری از ثبت نادرست حساب طرف مقابل، در این نسخه دریافت/پرداخت باید به فاکتور ثبت‌نهایی‌شده متصل باشد." });
    var alreadyPaid = await db.PaymentTransactions.ForTenant(tenantId)
        .Where(x => x.InvoiceId == invoice.Id)
        .SumAsync(x => (decimal?)x.Amount) ?? 0m;
    var remaining = invoice.Total - alreadyPaid;
    if (request.Amount > remaining)
        return Results.BadRequest(new { message = "مبلغ عملیات از مانده فاکتور بیشتر است.", invoiceTotal = invoice.Total, alreadyPaid, remaining });
    var counterpartType = invoice.Kind == InvoiceKinds.Sales ? "Asset" : "Liability";
    var counterpart = await db.Accounts.ForTenant(tenantId)
        .FirstOrDefaultAsync(a => a.Id == invoice.CounterpartyAccountId && a.IsActive && a.Type == counterpartType);
    if (counterpart is null || request.CounterpartyAccountId != invoice.CounterpartyAccountId)
        return Results.BadRequest(new { message = "حساب دریافتنی/پرداختنی انتخاب‌شده معتبر نیست یا با نوع فاکتور سازگار نیست." });
    var journalNumber = "PAY-" + number;
    if (await db.JournalEntries.ForTenant(tenantId).AnyAsync(x => x.DocumentNumber == journalNumber))
        return Results.Conflict(new { message = "شماره سند حسابداری مرتبط تکراری است؛ شماره رسید/پرداخت را تغییر دهید." });
    var journal = new JournalEntry { TenantId = tenantId, DocumentNumber = journalNumber,
        DocumentDate = DateTime.SpecifyKind(request.PaymentDate.Date, DateTimeKind.Utc),
        Description = (request.Kind == PaymentKinds.Receipt ? "دریافت " : "پرداخت ") + number,
        IsPosted = true };
    if (request.Kind == PaymentKinds.Receipt)
    {
        if (invoice.Kind != InvoiceKinds.Sales) return Results.BadRequest(new { message = "برای فاکتور خرید باید پرداخت ثبت کنید، نه دریافت." });
        journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = cash.Id, PartyId = party.Id, Debit = request.Amount, Description = "دریافت وجه" });
        journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = counterpart.Id, PartyId = party.Id, Credit = request.Amount, Description = "تسویه فاکتور فروش" });
    }
    else
    {
        if (invoice.Kind != InvoiceKinds.Purchase) return Results.BadRequest(new { message = "برای فاکتور فروش باید دریافت ثبت کنید، نه پرداخت." });
        journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = counterpart.Id, PartyId = party.Id, Debit = request.Amount, Description = "تسویه فاکتور خرید" });
        journal.Lines.Add(new JournalEntryLine { TenantId = tenantId, AccountId = cash.Id, PartyId = party.Id, Credit = request.Amount, Description = "پرداخت وجه" });
    }
    var payment = new PaymentTransaction { TenantId = tenantId, Number = number, Kind = request.Kind,
        PaymentDate = DateTime.SpecifyKind(request.PaymentDate.Date, DateTimeKind.Utc), PartyId = party.Id, CashAccountId = cash.Id,
        InvoiceId = invoice.Id, JournalEntry = journal, Amount = request.Amount, Notes = request.Notes?.Trim() };
    db.PaymentTransactions.Add(payment);
    db.JournalEntries.Add(journal);
    await db.SaveChangesAsync();
    return Results.Created($"/api/accounting/payments/{payment.Id}", new { message = "عملیات دریافت/پرداخت و سند " + journal.DocumentNumber + " ثبت شد.", payment.Id, journalEntryId = journal.Id });
}).RequireAuthorization("TenantWrite");


app.Run();

public sealed record UpdateAccountRequest(string? Code, string? Title, string? Type);
public sealed record UpdatePartyRequest(string? Name, string? Phone, string? Address);
