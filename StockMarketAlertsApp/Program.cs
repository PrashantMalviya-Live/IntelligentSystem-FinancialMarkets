using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client.Extensions.Msal;
using StockMarketAlertsApp.Clients;
using StockMarketAlertsApp.Components;
using StockMarketAlertsApp.Components.Account;
using StockMarketAlertsApp.Data;
using static System.Net.WebRequestMethods;
using Grpc.Core;
using Grpc.Net;
using Grpc.AspNetCore;
using StockMarketAlertsApp.Services;
using Microsoft.Extensions.Hosting;
using StockMarketAlertsApp.State;
using System;
using Microsoft.AspNetCore.Authentication.Google;
using StockMarketAlertsApp.Helper;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
.AddInteractiveServerComponents();

var serverApiUrl = builder.Configuration["ServerApiUrl"] ?? throw new Exception("Server Api Url is not set");
var loginServerApiUrl = builder.Configuration["LoginServerApiUrl"] ?? throw new Exception("Login Server Api Url is not set");

builder.Services.AddHttpClient<AlertsClient>(
    client => client.BaseAddress = new Uri(serverApiUrl));
builder.Services.AddHttpClient<MarketClient>(
    client => client.BaseAddress = new Uri(serverApiUrl));
builder.Services.AddHttpClient<TradesClient>(
    client => client.BaseAddress = new Uri(serverApiUrl));
builder.Services.AddHttpClient<AlgoClient>(
    client => client.BaseAddress = new Uri(serverApiUrl));
builder.Services.AddHttpClient<LoginClient>(
    client => client.BaseAddress = new Uri(loginServerApiUrl));


builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddScoped<GrpcService>();
builder.Services.AddScoped<NotifierService>();

//builder.Services.AddScoped<NotifierService>();
//builder.Services.AddScoped<GrpcService>();

//builder.Services.AddSingleton<ILoggerService, LoggerService>();
//builder.Services.AddSingleton<IOrderService, OrderService>();


builder.Services.AddAuthentication(
    options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        //options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    }

    )
    .AddCookie(Constants.AuthScheme, cookieOptions =>
    {
        cookieOptions.Cookie.Name = ".ap.user";
    })
    .AddGoogle(GoogleDefaults.AuthenticationScheme, googleOptions =>
    {
        //read: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-8.0  
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Google ClientId not found.");
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google Client not found.");
        googleOptions.CallbackPath = "/signin-google";
        googleOptions.SignInScheme = IdentityConstants.ExternalScheme;//.AuthenticationScheme;// Constants.AuthScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddBlazorBootstrap();

//builder.Services.AddGrpc();

//builder.Services.AddAuthorization(options =>
//{
//    options.FallbackPolicy = new AuthorizationPolicyBuilder()
//        .RequireAuthenticatedUser()
//        .Build();
//});

builder.Services.AddCors(o => o.AddPolicy("AllowAll", builder =>
{
    builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("GrpcHelper-Status", "GrpcHelper-Message", "GrpcHelper-Encoding", "GrpcHelper-Accept-Encoding");
}));


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BrokerAccountLoggedInOnly", policy => policy.RequireClaim("BrokerLogin"));
});
var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

//app.UseGrpcWeb();
//the gRPC-Web middleware can be configured so that all services support gRPC-Web by default and EnableGrpcWeb isn't required. 
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

//app.MapGrpcService<OrderService>().EnableGrpcWeb().RequireCors("AllowAll");
//app.MapGrpcService<LoggerService>().EnableGrpcWeb().RequireCors("AllowAll");


app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();


using var scope = app.Services.CreateScope();
var grpcService = scope.ServiceProvider.GetRequiredService<GrpcService>();

//var grpcService = app.Services.GetRequiredService<GrpcService>();
_ = Task.Run(async () => await grpcService.StartListeningAsync());

await app.RunAsync();
