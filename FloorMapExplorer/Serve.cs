#!/usr/bin/dotnet run

#:sdk Microsoft.NET.Sdk.Web

using Microsoft.AspNetCore.StaticFiles;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddCors();

WebApplication app = builder.Build();

app.UseCors(p =>
{
    p.AllowAnyOrigin();
});

app.UseDefaultFiles();

FileExtensionContentTypeProvider contentTypeProvider = new();
contentTypeProvider.Mappings.Add(".geojson", "application/json");
app.UseStaticFiles(new StaticFileOptions() { ContentTypeProvider = contentTypeProvider });

app.Run();