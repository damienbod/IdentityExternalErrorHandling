using IdentityExternalErrorHandling.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace IdentityExternalErrorHandling;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        builder.AddServiceDefaults();

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

                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnTicketReceived from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    await Task.CompletedTask;
                },
                OnRedirectToIdentityProvider = async context =>
                {
                    //context.ProtocolMessage.AcrValues = "http://schemas.openid.net/pape/policies/2007/06/multi-factor";

                    // Require some authentication context always for this app
                    //var claimsChallenge = "{\"id_token\":{\"acrs\":{\"essential\":true,\"value\":\"" + "C5" + "\"}}}";
                    //context.ProtocolMessage.Parameters.Add("claims", claimsChallenge);

                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnRedirectToIdentityProvider to identity provider. Scheme: {Scheme: }", context.Scheme.Name);
                    
                    await Task.CompletedTask;
                },
                OnMessageReceived = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnMessageReceived from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    if (!string.IsNullOrEmpty(context.ProtocolMessage.Error))
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/Error?remoteError={context.ProtocolMessage.Error}");
                    }

                    await Task.CompletedTask;
                },
                OnAccessDenied = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnAccessDenied from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    await Task.CompletedTask;
                },
                OnAuthenticationFailed = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnAuthenticationFailed from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    //context.HandleResponse();
                    //context.Response.Redirect($"/Error?remoteError={context.Exception.Message}");
                    await Task.CompletedTask;
                },
                OnRemoteFailure = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnRemoteFailure from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    if (context.Failure != null)
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/Error?remoteError={context.Failure.Message}");
                    }

                    await Task.CompletedTask;
                }
            };
        })
        .AddOpenIdConnect("keycloak", "keycloak", options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.SignOutScheme = IdentityConstants.ApplicationScheme;
            options.RemoteSignOutPath = new PathString("/signout-callback-oidc-keycloak");
            options.SignedOutCallbackPath = new PathString("/signout-oidc-keycloak");
            options.CallbackPath = new PathString("/signin-oidc-keycloak");

            options.Authority = builder.Configuration["AuthConfiguration:IdentityProviderUrl"];
            options.ClientSecret = builder.Configuration["AuthConfiguration:ClientSecret"];
            options.ClientId = builder.Configuration["AuthConfiguration:Audience"];
            options.ResponseType = OpenIdConnectResponseType.Code;

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.Scope.Add("offline_access");

            options.ClaimActions.Remove("amr");
            options.ClaimActions.MapJsonKey("website", "website");

            options.GetClaimsFromUserInfoEndpoint = true;
            options.SaveTokens = true;

            options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name",
                RoleClaimType = "role",
            };

            options.Events = new OpenIdConnectEvents
            {
                // Add event handlers
                OnTicketReceived = async context =>
                {
                    var idToken = context.Properties!.GetTokenValue("id_token");
                    var accessToken = context.Properties!.GetTokenValue("access_token");

                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnTicketReceived from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    await Task.CompletedTask;
                },
                OnRedirectToIdentityProvider = async context =>
                {
                    // Require passkeys
                    context.ProtocolMessage.AcrValues = "LoA3";

                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnRedirectToIdentityProvider to identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    await Task.CompletedTask;
                },
                OnMessageReceived = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnMessageReceived from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    if (!string.IsNullOrEmpty(context.ProtocolMessage.Error))
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/Error?remoteError={context.ProtocolMessage.Error}");
                    }

                    await Task.CompletedTask;
                },
                OnAccessDenied = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnAccessDenied from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    await Task.CompletedTask;
                },
                OnAuthenticationFailed = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnAuthenticationFailed from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

                    //context.HandleResponse();
                    //context.Response.Redirect($"/Error?remoteError={context.Exception.Message}");
                    await Task.CompletedTask;
                },
                OnRemoteFailure = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("OnRemoteFailure from identity provider. Scheme: {Scheme: }", context.Scheme.Name);

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
