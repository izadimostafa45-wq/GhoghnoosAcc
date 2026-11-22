using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// GHOGHNOOSACC_ACCOUNTING_VALIDATOR_REGISTRATION
builder.Services.AddSingleton<GhoghnoosAcc.Services.JournalEntryValidator>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<GhoghnoosAcc.Data.AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// GHOGHNOOSACC_ACCOUNTING_VALIDATOR_ENDPOINT
app.MapPost("/api/accounting/validate-entry",
    (GhoghnoosAcc.Services.JournalEntryValidationRequest request,
     GhoghnoosAcc.Services.JournalEntryValidator validator) =>
    {
        var result = validator.Validate(request);
        return result.IsValid
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });
app.Run();
