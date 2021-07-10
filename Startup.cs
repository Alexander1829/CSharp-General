using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Beeline.MobileId.Aggregator.Api.Controllers;
using Beeline.MobileId.Aggregator.BusinessLogic;
using Beeline.MobileId.Aggregator.BusinessLogic.Dal;
using Beeline.MobileId.Aggregator.BusinessLogic.Dal.Repositories;
using Beeline.MobileId.Aggregator.Common;
using Beeline.MobileId.Aggregator.Common.Logging;
using Beeline.MobileId.Aggregator.Db;
using Beeline.MobileId.Aggregator.Integrations.Discovery;
using Beeline.MobileId.Aggregator.Integrations.Idgw;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
			services.AddHealthChecks().AddSqlServer(Configuration["ApiApplicationSettings:AggregatorDb"], tags: new[] { "db" });
			services.AddMemoryCache();
			services.AddDbContext<AppDbContext>(options => options.UseNpgsql(Configuration["ApiApplicationSettings:ServiceDb"]));
			services.AddHangfire(x => x.UseSqlServerStorage(Configuration["ApiApplicationSettings:AggregatorDb"]));

			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new() { Title = "Aggregator platform api", Version = "v1" });
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

			services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(GetHttpHandlerSetter(s => s.Default));
			services.AddHttpClient<SIRequestAuthorizationService>();
			services.AddHttpClient<IdgwConnector>().ConfigurePrimaryHttpMessageHandler(GetHttpHandlerSetter(s => s.Idgw));
			services.AddHttpClient<DiscoveryConnector>().ConfigureHttpMessageHandlerBuilder((c) => c.PrimaryHandler = new HttpClientHandler() { AllowAutoRedirect = false, Proxy = new WebProxy(Configuration["ApiApplicationSettings:Proxies:Default:Address"]) { UseDefaultCredentials = true } });

			services.AddTransient<SIRequestValidationService>();
			services.AddTransient<AggregatorContext>();
			services.AddTransient<NfsRepository>();
			services.AddTransient<AppRepository>();
			services.AddTransient<CacheAccessor>();
			services.AddTransient<NfsService>();
			services.AddTransient<DIRequestAuthorizationService>();
			services.AddTransient<DIMCTokenService>();
			services.AddTransient<DIRequestValidationService>();
			services.AddTransient<StatePremiumInfoService>();
			services.AddTransient<PremiumInfoValidationService>();

			services.AddSingleton<IdgwConnectorManager>();

			services.Configure<Settings>(Configuration.GetSection("ApiApplicationSettings"));
		}

		static Func<IServiceProvider, HttpClientHandler> GetHttpHandlerSetter(Func<ProxySet, Proxy?> proxySelector) => sp =>
		{
			HttpClientHandler handler = new()
			{
				AllowAutoRedirect = false,
				DefaultProxyCredentials = CredentialCache.DefaultCredentials
			};
			var proxies = sp.GetRequiredService<IOptions<Settings>>().Value.Proxies;
			if (proxies != null)
			{
				var proxy = proxySelector(proxies);
				if (!string.IsNullOrWhiteSpace(proxy?.Address))
				{
					handler.Proxy = new WebProxy(proxy.Address);
					if (!string.IsNullOrWhiteSpace(proxy.UserName))
						handler.Proxy.Credentials = new NetworkCredential(proxy.UserName, proxy.Password, proxy.Domain);
				}
			}
			return handler;
		};

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseNLogHelper();
			app.MapRequesResponseLogger(
				new PathString[]
			{
				"/"
			}, new PathString[]
			{
				"/swagger"
			}
				);

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
				var error = httpContext.Features.Get<IExceptionHandlerPathFeature>().Error;
				if (error is UnifiedException unifiedException)
				{
					httpContext.Response.StatusCode = (int)OAuth2ErrorDetails.GetCode(unifiedException.Error);
					Dictionary<string, string> response = new() { [OpenIdConnectParameterNames.Error] = OAuth2ErrorDetails.GetText(unifiedException.Error) };
					if (unifiedException.ErrorDescription != null)
						response.Add(OpenIdConnectParameterNames.ErrorDescription, unifiedException.ErrorDescription);
					await httpContext.Response.WriteAsJsonAsync(response);
				}
				else
				{
					await httpContext.Response.WriteAsJsonAsync(new Dictionary<string, string> { [OpenIdConnectParameterNames.Error] = OAuth2ErrorDetails.GetText(OAuth2Error.ServerError) });
				}
			}));

			app.UseRouting();

			app.UseHangfireDashboard();

			app.UseAuthorization();

			app.UseAuthentication();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
				endpoints.MapHealthChecks("/p-health", new HealthCheckOptions());
				endpoints.MapHealthChecks("/p-health/db", new HealthCheckOptions() { Predicate = (check) => check.Tags.Contains("db") });
			});
		}
	}
}
