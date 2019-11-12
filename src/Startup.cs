﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

using openrmf_templates_api.Models;
using openrmf_templates_api.Data;
using openrmf_templates_api.Classes;

namespace openrmf_templates_api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Register the database components
            services.Configure<Settings>(options =>
            {
                options.ConnectionString = Environment.GetEnvironmentVariable("MONGODBCONNECTION");
                options.Database = Environment.GetEnvironmentVariable("MONGODB");
            });
            
            services.AddTransient<ITemplateRepository, TemplateRepository>();

            // Register the Swagger generator, defining one or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "OpenRMF Scoring API", Version = "v1", 
                    Description = "The Scoring API that goes with the OpenRMF tool",
                    Contact = new Contact
                    {
                        Name = "Dale Bingham",
                        Email = "dale.bingham@cingulara.com",
                        Url = "https://github.com/Cingulara/openrmf-api-template"
                    } });
            });

            // add the authentication JWT check that has AuthN and AuthZ in it for the roles needed
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(o =>
            {
                o.Authority = Environment.GetEnvironmentVariable("JWT-AUTHORITY");
                o.Audience = Environment.GetEnvironmentVariable("JWT-CLIENT");
                o.IncludeErrorDetails = true;
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidIssuer = Environment.GetEnvironmentVariable("JWT-AUTHORITY"),
                    ValidateLifetime = true
                };

                o.Events = new JwtBearerEvents()
                {
                    OnAuthenticationFailed = c =>
                    {
                        c.NoResult();
                        c.Response.StatusCode = 401;
                        c.Response.ContentType = "text/plain";

                        return c.Response.WriteAsync(c.Exception.ToString());
                    }
                };
            });

            // setup the RBAC for this
            services.AddAuthorization(options =>
            {
                options.AddPolicy("Administrator", policy => policy.RequireRole("roles", "[Administrator]"));
                options.AddPolicy("Editor", policy => policy.RequireRole("roles", "[Editor]"));
                options.AddPolicy("Reader", policy => policy.RequireRole("roles", "[Reader]"));
                options.AddPolicy("Assessor", policy => policy.RequireRole("roles", "[Assessor]"));
            });

            // ********************
            // USE CORS
            // ********************
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder
                        .AllowAnyOrigin() 
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                    });
            });
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddXmlSerializerFormatters();

            // add this in memory for now. Persist later.
        	services.AddDistributedMemoryCache();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenRMF Template API V1");
            });

            // ********************
            // USE CORS
            // ********************
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseHttpsRedirection();
            app.UseMvc();

            // load the templates from CKL files
            if (DefaultTemplateLoader.LoadTemplates()) {
                // log the loading was successful
            } 
            else {
                // Log it was not successful
            }
        }   
    }
}
