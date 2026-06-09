using cpms_API.Middleware;
using cpms_Application;
using cpms_Application.Interfaces;
using cpms_Application.MyMapper;
using cpms_Application.Services;
using cpms_Domain;
using cpms_Infrastructure;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
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
// CONFIGURATION
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
    options.SuppressModelStateInvalidFilter = true;
});

// Kích hoạt tính năng tự động validate đầu vào của FluentValidation trên Controller
builder.Services.AddFluentValidationAutoValidation();

// Nạp toàn bộ các class Validator (bao gồm cả SubPlanValidator) trong tầng Application vào DI Container
//builder.Services.AddValidatorsFromAssemblyContaining<SubPlanValidator>();

// ======================================================
// DATABASE (Đã fix lỗi Design-time Migration)
// ======================================================
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);

    options.ConfigureWarnings(warnings =>
        warnings.Ignore(CoreEventId.NavigationBaseIncludeIgnored));
});

// ======================================================
// JWT AUTHENTICATION (Đã fix cách đọc dữ liệu từ JSON)
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
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AntiPhisher API", Version = "v1" });

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

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddAutoMapper(typeof(MapperConfigurationsProfile).Assembly);
builder.Services.AddSingleton(configuration!);
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Services Registration
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IClaimService, ClaimService>();
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseMiddleware<ValidationMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

// Configure the HTTP request pipeline.
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
