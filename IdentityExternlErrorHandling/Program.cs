using IdentityExternalErrorHandling.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;

namespace IdentityExternalErrorHandling;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddEntityFrameworkStores<ApplicationDbContext>();

        builder.Services.AddRazorPages();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddOpenIdConnect("EntraID", "EntraID", oidcOptions =>
        {
            oidcOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            oidcOptions.Scope.Add(OpenIdConnectScope.OpenIdProfile);
            oidcOptions.Scope.Add("user.read");
            oidcOptions.Scope.Add(OpenIdConnectScope.OfflineAccess);
            oidcOptions.Authority = $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0/";
            oidcOptions.ClientId = builder.Configuration["AzureAd:ClientId"];
            oidcOptions.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
            oidcOptions.ResponseType = OpenIdConnectResponseType.Code;
            oidcOptions.MapInboundClaims = false;
            oidcOptions.SaveTokens = true;
            oidcOptions.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Name;
            oidcOptions.TokenValidationParameters.RoleClaimType = "role";

            oidcOptions.Events = new OpenIdConnectEvents
            {
                // Add event handlers            
                OnMessageReceived = async context =>
                {
                    if (!string.IsNullOrEmpty(context.ProtocolMessage.Error))
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/Error?remoteError={context.ProtocolMessage.Error}");
                    }

                    await Task.CompletedTask;
                }
            };
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
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

        app.Run();
    }
}
