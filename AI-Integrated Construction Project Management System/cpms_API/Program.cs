using cpms_API.Middleware;
using cpms_Application;
using cpms_Application.Interfaces;
using cpms_Application.MyMapper;
using cpms_Application.Services;
using cpms_Domain;
using cpms_Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using cpms_API.BackgroundServices;
using cpms_API.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// CONFIGURATION & APPSETTINGS
// ======================================================
var configuration = builder.Configuration.Get<AppSetting>();
if (configuration != null)
{
    builder.Services.AddSingleton(configuration);
}

// ======================================================
// CONTROLLERS & VALIDATION
// ======================================================
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = false;
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e =>
                string.IsNullOrWhiteSpace(e.ErrorMessage) ? "The supplied value is invalid." : e.ErrorMessage).ToArray());
        return new BadRequestObjectResult(new cpms_Application.Response.ApiResponse()
            .SetBadRequest(result: errors, message: "Request validation failed."));
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var value in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
        if (IPAddress.TryParse(value, out var address)) options.KnownProxies.Add(address);
});

// Kích hoạt tính năng tự động validate đầu vào của FluentValidation trên Controller
builder.Services.AddFluentValidationAutoValidation();

// 🚀 SỬA/MỞ LẠI: Nạp toàn bộ các class Validator từ tầng Application để FluentValidation hoạt động
// Thay 'MapperConfigurationsProfile' bằng bất kỳ class nào nằm trong tầng Application để nó quét qua Assembly đó
builder.Services.AddValidatorsFromAssemblyContaining<MapperConfigurationsProfile>();

// ======================================================
// DATABASE CONFIGURATION
// ======================================================
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("cpms_Infrastructure")); // 🚀 ƯU TIÊN: Chỉ định rõ Assembly chứa Migration để tránh lỗi Command-line

    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(CoreEventId.NavigationBaseIncludeIgnored));
});

// ======================================================
// JWT AUTHENTICATION
// ======================================================
var secretValue = builder.Configuration["SecretToken:Value"];
if (string.IsNullOrWhiteSpace(secretValue) || Encoding.UTF8.GetByteCount(secretValue) < 64)
{
    throw new Exception("SecretToken:Value must contain at least 64 bytes for HS512 signing.");
}
if (configuration == null || string.IsNullOrWhiteSpace(configuration.SecretToken.Issuer) ||
    string.IsNullOrWhiteSpace(configuration.SecretToken.Audience) || configuration.SecretToken.DurationInMinutes <= 0)
    throw new Exception("SecretToken issuer, audience, and duration must be configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretValue)),
            ValidateIssuer = true,
            ValidIssuer = configuration!.SecretToken.Issuer,
            ValidateAudience = true,
            ValidAudience = configuration.SecretToken.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var roleValue = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
                if (!int.TryParse(userIdValue, out var userId))
                {
                    context.Fail("The token does not contain a valid user identifier.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var account = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
                if (account == null || account.IsEmailVerified != true ||
                    !string.Equals(account.Role.ToString(), roleValue, StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("The account is inactive or its authorization has changed. Sign in again.");
                    return;
                }
                var passwordVersion = context.Principal?.FindFirst("pwd")?.Value;
                if (!long.TryParse(passwordVersion, out var issuedPasswordTicks) || issuedPasswordTicks != account.PasswordChangedAt.Ticks)
                    context.Fail("The password changed after this token was issued. Sign in again.");
            }
        };
    });

// ======================================================
// SWAGGER / OPENAPI
// ======================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BuildSense API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ======================================================
// CORE SERVICES INFRASTRUCTURE & MAPPING
// ======================================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ITeamsMeetingClient, TeamsMeetingClient>();
builder.Services.AddHttpClient<IGoogleAIClient, GoogleAIClient>();
builder.Services.AddAutoMapper(typeof(MapperConfigurationsProfile).Assembly);
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ======================================================
// APPLICATION SERVICES REGISTRATION
// ======================================================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IClaimService, ClaimService>();
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ISupplierRecommendationService, SupplierRecommendationService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProgressReportService, ProgressReportService>();
builder.Services.AddScoped<IMaterialRequestService, MaterialRequestService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();


// ======================================================
// CORS POLICY
// ======================================================
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    options.AddPolicy("Frontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        else if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// ======================================================
// HTTP REQUEST PIPELINE (MIDDLEWARES)
// ======================================================
// 💡 Lưu ý: Đặt Middleware Custom trước để bắt lỗi toàn cục cho pipeline
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<ValidationMiddleware>();
app.UseForwardedHeaders();

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}


app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BuildSense API v1");
    c.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseMiddleware<DistributedAuthRateLimitMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        });
    }
});

app.Run();
