using System.IdentityModel.Tokens.Jwt;
using System.Text;
using EventTicketing.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EventTicketing.Services;
using EventTicketing.Services.Orders;
using EventTicketing.Services.Payments;
using EventTicketing.Services.Pricing;
using EventTicketing.Services.Tickets;
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

builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ITicketService, TicketService>();

// Pick one gateway implementation to start
builder.Services.AddScoped<IPaymentGateway, FakeGateway>();

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
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsEventOwner", policy =>
        policy.Requirements.Add(new EventTicketing.Services.EventOwnerRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, JWTAuth.Services.EventOwnerHandler>();
builder.Services.AddHttpContextAccessor(); 

builder.Services.AddResponseCaching();

var app = builder.Build();

// add middleware BEFORE endpoints
app.UseResponseCaching();

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