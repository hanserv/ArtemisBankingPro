using Asp.Versioning;
using Microsoft.OpenApi.Models;

namespace ArtemisBankingPro.Api.Extensions
{
    public static class ServiceExtension
    {
        public static void AddSwaggerExtension(this IServiceCollection services)
        {

            services.AddSwaggerGen(options =>
            {
                List<string> xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", searchOption: SearchOption.TopDirectoryOnly).ToList();
                xmlFiles.ForEach(xmlFile => options.IncludeXmlComments(xmlFile));

                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1.0",
                    Title = "ArtemisBankingPro API",
                    Description = "This Api will be responsible for overall data distribution",
                    Contact = new OpenApiContact
                    {
                        Name = "Hanser Ventura",
                        Email = "20251027@itla.edu.do",
                    }
                });

                options.DescribeAllParametersInCamelCase();
                options.EnableAnnotations();

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    Description = "Input your Bearer token in this format 'Bearer { your token here }'"
                });


                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "Bearer",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        }, new List<string>()
                    }
                });
            });
        }

        public static void AddAppiVersioningExtension(this IServiceCollection services)
        {
            services.AddApiVersioning(opt =>
            {
                opt.DefaultApiVersion = new ApiVersion(1, 0);
                opt.AssumeDefaultVersionWhenUnspecified = true;
                opt.ReportApiVersions = true;
                opt.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new HeaderApiVersionReader("X-Api-Version")
                );
            }).AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV"; // Debe quedar así (con comillas simples)
                options.SubstituteApiVersionInUrl = true;
            });
        }
    }
}
