using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Beeline.MobileId.Aggregator.Api.Logging;
using Beeline.MobileId.Aggregator.BusinessLogic;
using Beeline.MobileId.Aggregator.BusinessLogic.Dal;
using Beeline.MobileId.Aggregator.BusinessLogic.Dal.Repositories;
using Beeline.MobileId.Aggregator.Common;
using Beeline.MobileId.Aggregator.Cryptography;
using Beeline.MobileId.Aggregator.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.OpenApi.Models;
using static Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions;

namespace Beeline.MobileId.Aggregator.Api
{
	public class Startup
	{
		public IConfiguration Configuration { get; }

		public Startup(IConfiguration configuration) => Configuration = configuration;

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddControllers();
			services.AddMemoryCache();
			services.AddDbContext<AppDbContext>(options => options.UseNpgsql(Configuration["ApplicationSettings:ServiceDb"]));

			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new() { Title = "Agreagator platform api", Version = "v1" });
				c.AddSecurityDefinition("basic", new()
				{
					Name = "Authorization",
					Type = SecuritySchemeType.Http,
					Scheme = "basic",
					In = ParameterLocation.Header,
					Description = "Basic Authorization header using the Bearer scheme."
				});

				c.AddSecurityRequirement(new()
				{
					{
						new()
						{
							Reference = new()
							{
								Type = ReferenceType.SecurityScheme,
								Id = "basic"
							}
						},
						new string[] { }
					}
				});
				c.AddSecurityDefinition("Bearer",
					new()
					{
						In = ParameterLocation.Header,
						Description = "Please enter into field the word 'Bearer' following by space and JWT",
						Name = "Authorization",
						Type = SecuritySchemeType.ApiKey
					});
				c.AddSecurityRequirement(new()
				{
					{
						new()
						{
							Reference = new()
							{
								Type = ReferenceType.SecurityScheme,
								Id = "Bearer"
							},
							Scheme = "oauth2",
							Name = "Bearer",
							In = ParameterLocation.Header
						},
						new List<string>()
					}
				});
			});

			services.AddTransient(delegate
			{
				Settings settings = new();
				new ConfigurationBuilder().AddJsonFile("appsettings.json", false).Build().GetSection("ApplicationSettings").Bind(settings);
				return settings;
			});

			services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(sp =>
			{
				var settings = sp.GetRequiredService<Settings>();
				HttpClientHandler handler = new() { DefaultProxyCredentials = CredentialCache.DefaultCredentials };
				if (!string.IsNullOrWhiteSpace(settings.Proxy?.Address))
				{
					handler.Proxy = new WebProxy(settings.Proxy.Address);
					if (!string.IsNullOrWhiteSpace(settings.Proxy.UserName))
						handler.Proxy.Credentials = new NetworkCredential(settings.Proxy.UserName, settings.Proxy.Password, settings.Proxy.Domain);
				}
				return handler;
			});
			services.AddHttpClient<SIRequestAuthorizationService>();
			services.AddHttpClient<IdGatewayConnector>();

			services.AddTransient<SIRequestValidationService>();
			services.AddTransient<JwtSignatureValidator>();
			services.AddTransient<AggregatorContext>();
			services.AddTransient<NfsRepository>();
			services.AddTransient<AppRepository>();
			services.AddTransient<CacheAccessor>();
			services.AddTransient<NfsService>();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseSwagger();

			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aggregator platform api");
			});

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseExceptionHandler(applicationBuilder => applicationBuilder.Run(async httpContext =>
			{
				if (httpContext.Features.Get<IExceptionHandlerPathFeature>().Error is UnifiedException unifiedException)
				{
					httpContext.Response.StatusCode = (int)OAuth2ErrorDetails.GetCode(unifiedException.Error);
					Dictionary<string, string> response = new() { [OpenIdConnectParameterNames.Error] = OAuth2ErrorDetails.GetText(unifiedException.Error) };
					if (unifiedException.ErrorDescription != null)
						response.Add(OpenIdConnectParameterNames.ErrorDescription, unifiedException.ErrorDescription);
					await httpContext.Response.WriteAsJsonAsync(response);
				}
				else
					await httpContext.Response.WriteAsJsonAsync(new Dictionary<string, string> { [OpenIdConnectParameterNames.Error] = OAuth2ErrorDetails.GetText(OAuth2Error.ServerError) });
			}));

			app.UseRouting();

			app.UseMiddleware<LoggingMiddleware>();

			app.UseAuthorization();

			app.UseAuthentication();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
			});
		}
	}
}
