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
    Guid? accountId,
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

public sealed record UpdateAccountRequest(string? Code, string? Title, string? Type);
public sealed record UpdatePartyRequest(string? Name, string? Phone, string? Address);

app.Run();
