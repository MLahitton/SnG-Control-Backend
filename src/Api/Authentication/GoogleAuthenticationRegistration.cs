using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SnG.Control.Application.Identity;

namespace SnG.Control.Api.Authentication;

public static class GoogleAuthenticationRegistration
{
    public static IServiceCollection AddGoogleAuthentication(this IServiceCollection services)
    {
        services.AddOptions<GoogleAuthenticationOptions>().BindConfiguration(GoogleAuthenticationOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "Configure Authentication:Google:ClientId.")
            .ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<GoogleAuthenticationOptions>>((options, google) =>
            {
                options.Authority = "https://accounts.google.com";
                options.Audience = google.Value.ClientId;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                    ValidateAudience = true,
                    ValidAudience = google.Value.ClientId,
                    IgnoreTrailingSlashWhenValidatingAudience = false,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(60),
                    NameClaimType = "name",
                    IncludeTokenOnFailedValidation = false,
                    LogTokenId = false
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Principal?.FindFirst("sub")?.Value))
                            context.Fail("The identity does not contain a valid subject.");
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build());
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        return services;
    }
}
