using IdentityExternalErrorHandling.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

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

        // Identity.External
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddOpenIdConnect("EntraID", "EntraID", oidcOptions =>
        {
            oidcOptions.SignInScheme = IdentityConstants.ExternalScheme;
            oidcOptions.SignOutScheme = IdentityConstants.ApplicationScheme;
            oidcOptions.RemoteSignOutPath = new PathString("/signout-callback-oidc-entra");
            oidcOptions.SignedOutCallbackPath = new PathString("/signout-oidc-entra");
            oidcOptions.CallbackPath = new PathString("/signin-oidc-entra");

            oidcOptions.Scope.Add("user.read");
            oidcOptions.Authority = $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0/";
            oidcOptions.ClientId = builder.Configuration["AzureAd:ClientId"];
            oidcOptions.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
            oidcOptions.ResponseType = OpenIdConnectResponseType.Code;
            oidcOptions.UsePkce = true;

            oidcOptions.MapInboundClaims = false;
            oidcOptions.SaveTokens = true;
            oidcOptions.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Name;
            oidcOptions.TokenValidationParameters.RoleClaimType = "role";

            oidcOptions.Events = new OpenIdConnectEvents
            {
                // Add event handlers
                OnTicketReceived = async context =>
                {
                    var idToken = context.Properties!.GetTokenValue("id_token");
                    var accessToken = context.Properties!.GetTokenValue("access_token");

                    await Task.CompletedTask;
                },
                OnRedirectToIdentityProvider = async context =>
                {
                    //context.ProtocolMessage.AcrValues = "p2";
                    //context.ProtocolMessage.State = "fail";
                    await Task.CompletedTask;
                },
                OnMessageReceived = async context =>
                {
                    if (!string.IsNullOrEmpty(context.ProtocolMessage.Error))
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/Error?remoteError={context.ProtocolMessage.Error}");
                    }

                    await Task.CompletedTask;
                },
                OnAccessDenied = async context =>
                {
                    await Task.CompletedTask;
                },
                OnAuthenticationFailed = async context =>
                {
                    //context.HandleResponse();
                    //context.Response.Redirect($"/Error?remoteError={context.Exception.Message}");
                    await Task.CompletedTask;
                },
                OnRemoteFailure = async context =>
                {
                    if (context.Failure != null)
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/Error?remoteError={context.Failure.Message}");
                    }

                    await Task.CompletedTask;
                }
            };
        });

        var app = builder.Build();

        IdentityModelEventSource.ShowPII = true;
        IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

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
