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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// CONFIGURATION & APPSETTINGS
// ======================================================
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

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
    options.SuppressModelStateInvalidFilter = true;
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
if (string.IsNullOrWhiteSpace(secretValue))
{
    throw new Exception("SecretToken:Value is missing or invalid in appsettings.json");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretValue)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
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
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// ======================================================
// HTTP REQUEST PIPELINE (MIDDLEWARES)
// ======================================================
// 💡 Lưu ý: Đặt Middleware Custom trước để bắt lỗi toàn cục cho pipeline
app.UseMiddleware<ValidationMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
