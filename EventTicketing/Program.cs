using System.Text;
using EventTicketing.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EventTicketing.Services;
using JWTAuth.Services;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("Default"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Default"))));

builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<EventTicketing.Entities.User>,
    Microsoft.AspNetCore.Identity.PasswordHasher<EventTicketing.Entities.User>>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<EventTicketing.Entities.User>,
    Microsoft.AspNetCore.Identity.PasswordHasher<EventTicketing.Entities.User>>();

// Token service
builder.Services.AddScoped<ITokenService, TokenService>();

// AuthN (JWT)
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// AuthZ (roles + custom policies)
builder.Services.AddAuthorization(options =>
{
    // Example custom policy for organizer ownership checks
    options.AddPolicy("IsEventOwner", policy =>
        policy.Requirements.Add(new EventTicketing.Services.EventOwnerRequirement()));
});

// ✅ correct lifetimes
builder.Services.AddScoped<IAuthorizationHandler, JWTAuth.Services.EventOwnerHandler>();
builder.Services.AddHttpContextAccessor(); // if not already present



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();