using System.Text.Json.Serialization;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Interfaces;
using Dixon.LabelVerification.API.Middleware;
using Dixon.LabelVerification.API.Repositories;
using Dixon.LabelVerification.API.Services;
using Dixon.LabelVerification.API.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Dixon.LabelVerification.API.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var connectionString = builder.Configuration.GetConnectionString(DatabaseConfiguration.ConnectionStringName)
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");

builder.Services.AddDbContext<LabelVerificationDbContext>(options =>
    options.UseSqlServer(connectionString));
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer must be configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience must be configured.");
var configuredJwtSigningKey = builder.Configuration["Jwt:SigningKey"];
var generatedDevelopmentSigningKey = builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(configuredJwtSigningKey);
var jwtSigningKey = generatedDevelopmentSigningKey
    ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
    : configuredJwtSigningKey
        ?? throw new InvalidOperationException("Jwt:SigningKey must be configured through a secret store or environment.");
if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
builder.Configuration["Jwt:SigningKey"] = jwtSigningKey;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var role = context.Principal?.FindFirst(
                    System.Security.Claims.ClaimTypes.Role)?.Value;
                if (!Guid.TryParse(userIdValue, out var userId) || !ApplicationRoles.IsValid(role))
                {
                    context.Fail("The account token is invalid.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<LabelVerificationDbContext>();
                var user = await db.Users.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == userId, context.HttpContext.RequestAborted);
                var brandIdValue = context.Principal?.FindFirst("brand_id")?.Value;
                var hasValidBrandClaim = role == ApplicationRoles.Admin
                    ? brandIdValue is null && user?.BrandId is null
                    : Guid.TryParse(brandIdValue, out var tokenBrandId)
                        && user?.BrandId == tokenBrandId;
                if (user is null
                    || !string.Equals(user.Role, role, StringComparison.Ordinal)
                    || !hasValidBrandClaim
                    || !ApplicationRoles.HasValidBrandAssignment(user.Role, user.BrandId))
                    context.Fail("The account or its role/brand assignment is invalid or has changed.");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticatedUserContext>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<PasswordResetEmailSender>();
builder.Services.AddScoped<IFieldComparisonService, FieldComparisonService>();
builder.Services.AddScoped<IInspectionWorkflowService, InspectionWorkflowService>();
builder.Services.AddScoped<IInspectionSessionRepository, InspectionSessionRepository>();
builder.Services.AddScoped<IInspectionManagementService, InspectionManagementService>();
builder.Services.AddScoped<IMasterLabelService, MasterLabelService>();
builder.Services.AddScoped<IInspectionReportService, InspectionReportService>();
builder.Services.AddScoped<IInspectionAccessService, InspectionAccessService>();
builder.Services.AddHostedService<InspectionJobRecoveryService>();
builder.Services.AddScoped<IBrandConfigurationService, BrandConfigurationService>();
builder.Services.AddScoped<IModelConfigurationService, ModelConfigurationService>();
builder.Services.AddScoped<ILabelProfileConfigurationService, LabelProfileConfigurationService>();
builder.Services.AddScoped<IFieldDefinitionConfigurationService, FieldDefinitionConfigurationService>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient("OcrService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OCR:BaseUrl"] ?? "http://127.0.0.1:8000/");
    client.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddCors(options => options.AddPolicy("ViteDevelopment", policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (generatedDevelopmentSigningKey)
{
    app.Logger.LogWarning(
        "No Jwt:SigningKey was configured. A temporary development-only signing key was generated; existing tokens will be invalid after restart.");
}

if (args.Contains("--bootstrap-admin", StringComparer.Ordinal))
{
    var fullName = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME");
    var username = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME");
    var email = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL");
    var password = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD");
    if (string.IsNullOrWhiteSpace(fullName)
        || string.IsNullOrWhiteSpace(username)
        || string.IsNullOrWhiteSpace(email)
        || string.IsNullOrWhiteSpace(password)
        || password.Length < 12)
    {
        throw new InvalidOperationException(
            "Set BOOTSTRAP_ADMIN_FULL_NAME, BOOTSTRAP_ADMIN_USERNAME, BOOTSTRAP_ADMIN_EMAIL, and a 12+ character BOOTSTRAP_ADMIN_PASSWORD to bootstrap an Admin account.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<LabelVerificationDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    await using var transaction = await db.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.Serializable);
    if (await db.Users.AnyAsync(x => x.Role == ApplicationRoles.Admin))
        throw new InvalidOperationException("An Admin account already exists.");

    var admin = new User
    {
        FullName = fullName.Trim(),
        Username = username.Trim(),
        Email = email.Trim(),
        Role = ApplicationRoles.Admin,
        PasswordHash = string.Empty
    };
    admin.PasswordHash = passwordHasher.HashPassword(admin, password);
    db.Users.Add(admin);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    Console.WriteLine($"Admin '{admin.Username}' created.");
    return;
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("ViteDevelopment");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
