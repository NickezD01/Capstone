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
});

// Kích hoạt tính năng tự động validate đầu vào của FluentValidation trên Controller
builder.Services.AddFluentValidationAutoValidation();

// 🚀 SỬA/MỞ LẠI: Nạp toàn bộ các class Validator từ tầng Application để FluentValidation hoạt động
// Thay 'MapperConfigurationsProfile' bằng bất kỳ class nào nằm trong tầng Application để nó quét qua Assembly đó
builder.Services.AddValidatorsFromAssemblyContaining<MapperConfigurationsProfile>();

// ======================================================
// DATABASE CONFIGURATION
// ======================================================
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("cpms_Infrastructure")); // 🚀 ƯU TIÊN: Chỉ định rõ Assembly chứa Migration để tránh lỗi Command-line

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
                    context.Fail("The account is inactive or its authorization has changed. Sign in again.");
            }
        };
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"{context.Connection.RemoteIpAddress}:{context.Request.Path}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
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
builder.Services.AddAutoMapper(_ => { }, typeof(MapperConfigurationsProfile).Assembly);
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
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProgressReportService, ProgressReportService>();
builder.Services.AddScoped<IMaterialRequestService, MaterialRequestService>();
builder.Services.AddScoped<IWarehouseTransferService, WarehouseTransferService>();


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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
