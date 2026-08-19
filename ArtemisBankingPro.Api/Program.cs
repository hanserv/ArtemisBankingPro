using System.Text.Json.Serialization;
using ArtemisBankingPro.Api.Extensions;
using ArtemisBankingPro.Api.Handlers;
using ArtemisBankingPro.Core.Application;
using ArtemisBankingPro.Infrastructure.Identity;
using ArtemisBankingPro.Infrastructure.Persistence;
using ArtemisBankingPro.Infrastructure.Shared;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext().CreateLogger(); 

builder.Host.UseSerilog(Log.Logger);

// Add services to the container.

builder.Services.AddControllers(opt =>
{
    opt.Filters.Add(new ProducesAttribute("application/json"));
}).ConfigureApiBehaviorOptions(opt =>
{
    opt.SuppressInferBindingSourcesForParameters = true;
    opt.SuppressMapClientErrors = true;
}).AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddPersistenceLayer(builder.Configuration);
builder.Services.AddApplicationLayer();
builder.Services.AddSharedLayer(builder.Configuration);
builder.Services.AddIdentityLayerForApi(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddAppiVersioningExtension();
builder.Services.AddSwaggerExtension();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
await app.Services.RunIdentitySeedAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerExtension(app);
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();
app.UseHealthChecks("/health");

app.MapControllers();

app.Run();
