using IdentityExternalErrorHandling.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
                }
            };
        })
        .AddOpenIdConnect("Auth0", "Auth0", options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.SignOutScheme = IdentityConstants.ApplicationScheme;
            options.CallbackPath = new PathString("/signin-oidc-auth0");
            options.RemoteSignOutPath = new PathString("/signout-callback-oidc-auth0");
            options.SignedOutCallbackPath = new PathString("/signout-oidc-auth0");

            options.Authority = $"https://{builder.Configuration["Auth0:Domain"]}";
            options.ClientId = builder.Configuration["Auth0:ClientId"];
            options.ClientSecret = builder.Configuration["Auth0:ClientSecret"];
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.Scope.Add("auth0-user-api-one");
            options.ClaimsIssuer = "Auth0";
            options.SaveTokens = true;
            options.UsePkce = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.TokenValidationParameters.NameClaimType = "name";

            options.Events = new OpenIdConnectEvents
            {
                OnTokenResponseReceived = context =>
                {
                    var idToken = context.TokenEndpointResponse.IdToken;
                    return Task.CompletedTask;
                },
                // handle the logout redirection 
                OnRedirectToIdentityProviderForSignOut = (context) =>
                {
                    var logoutUri = $"https://{builder.Configuration["Auth0:Domain"]}/v2/logout?client_id={builder.Configuration["Auth0:ClientId"]}";

                    var postLogoutUri = context.Properties.RedirectUri;
                    if (!string.IsNullOrEmpty(postLogoutUri))
                    {
                        if (postLogoutUri.StartsWith("/"))
                        {
                            // transform to absolute
                            var request = context.Request;
                            postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase + postLogoutUri;
                        }
                        logoutUri += $"&returnTo={Uri.EscapeDataString(postLogoutUri)}";
                    }

                    context.Response.Redirect(logoutUri);
                    context.HandleResponse();

                    return Task.CompletedTask;
                },
                OnRedirectToIdentityProvider = context =>
                {
                    // The context's ProtocolMessage can be used to pass along additional query parameters
                    // to Auth0's /authorize endpoint.
                    // 
                    // Set the audience query parameter to the API identifier to ensure the returned Access Tokens can be used
                    // to call protected endpoints on the corresponding API.
                    context.ProtocolMessage.SetParameter("audience", "https://auth0-api1");
                    context.ProtocolMessage.AcrValues = "http://schemas.openid.net/pape/policies/2007/06/multi-factor";

                    return Task.FromResult(0);
                },
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
