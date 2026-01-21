using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder
    .AddGraphQL()
    .AddSampleTypes();

var app = builder.Build();
app.Run();