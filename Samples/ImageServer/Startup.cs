using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImageServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ImageServer
{
    public class Startup
    {
        string UploadRouteUrl;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews().AddNewtonsoftJson();

            var config = new AppConfig();

            var appConfigService = new AppConfigService()
            {
                Config = config
            };

            services.AddSingleton<IAppConfigService>(appConfigService);

            services.AddResponseCaching();

            services.AddImageGo();

            var configSection = Configuration.GetSection("Images");
            config.ApiKey = configSection.GetValue<string>("ApiKey");
            config.AllowFolders = configSection.GetValue<string>("AllowFolders");
            config.AllowAllExtensions = configSection.GetValue<string>("AllowAllExtensions");
            config.AllowLocalIpUploadOnly = configSection.GetValue<string>("AllowLocalIpUploadOnly");
            UploadRouteUrl = configSection.GetValue<string>("UploadRouteUrl");

            var s = configSection.GetSection("He");

            var hi = Configuration.GetSection("He").GetValue<string>("Hi");
            Console.WriteLine("hi1"+hi);
            var hi2 = Configuration.GetValue<string>("He:Hi");
            Console.WriteLine("hi2"+hi2);
            var hi3 =Configuration.GetSection("He").GetValue<string>("Hi");
            Console.WriteLine("hi3"+hi3);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }


            // app.UseHttpsRedirection();

            app.UseResponseCaching();

            app.Use(async (context, next) =>
                    {
                        context.Response.GetTypedHeaders().CacheControl =
                            new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                            {
                                Public = false,
                                MaxAge = TimeSpan.FromSeconds(10),
                                

                            };
                        context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
                            new string[] { "Accept-Encoding" };
                        await next();
                    });


            app.UseImageGo();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                var url = string.IsNullOrWhiteSpace(UploadRouteUrl) ? "" : UploadRouteUrl + "/";
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                endpoints.MapControllerRoute(
                    name: "api",
                    pattern: "Api",
                    defaults: new { controller = "Upload", action = "Index" });
            });

        }


        private string GetSpeed(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Length > 1)
            {
                switch (value[1])
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                        return value[1].ToString();
                }
            }
            return "1";
        }

        private string GetSizeValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "0") return "10000";
            return value;
        }

        private string GetMode(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                switch (value[0])
                {
                    case 'c':
                        return "crop";
                    case 'm':
                        return "max";
                    case 'p':
                        return "pad";
                }
            }
            return "crop";
        }

    }
}
