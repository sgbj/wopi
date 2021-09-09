using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Wopi;

var builder = WebApplication.CreateBuilder(args);

using var httpClient = new HttpClient();
var discoveryXml = await httpClient.GetStringAsync(builder.Configuration["Wopi:DiscoveryUrl"]);
var discoveryDocument = XDocument.Parse(discoveryXml);

builder.Services.AddDbContext<WopiDbContext>(options =>
    options.UseInMemoryDatabase("Wopi"));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/files", async (HttpContext context, WopiDbContext db) =>
{
    var files = await db.WopiFiles.Select(f => new { f.Id, f.Name }).ToListAsync();

    return files.Select(f =>
    {
        var extension = Path.GetExtension(f.Name);

        if (extension.StartsWith("."))
        {
            extension = extension[1..];
        }

        return new
        {
            f.Id,
            f.Name,
            ViewUrl = GetUrl("view", extension),
            EditUrl = GetUrl("edit", extension)
        };

        string? GetUrl(string name, string extension)
        {
            var urlsrc = discoveryDocument
                ?.Descendants("action")
                .FirstOrDefault(e => e.Attribute("name")?.Value == name && e.Attribute("ext")?.Value == extension)
                ?.Attribute("urlsrc")?.Value
                ?? "";

            if (string.IsNullOrWhiteSpace(urlsrc))
            {
                return null;
            }

            urlsrc = Regex.Replace(urlsrc, "<[^>]+&>", "");
            urlsrc += !urlsrc.EndsWith("?") && !urlsrc.EndsWith("&") ? "?" : "";
            return urlsrc + "WOPISrc=" + Uri.EscapeDataString($"{context.Request.Scheme}://{context.Request.Host}/wopi/files/{f.Id}");
        }
    });
});

app.MapPost("/api/files", async (WopiDbContext db, WopiFile file) =>
{
    file.Id ??= Guid.NewGuid().ToString();
    file.Version ??= Guid.NewGuid().ToString();

    db.Add(file);
    await db.SaveChangesAsync();

    return Results.Created($"/api/files/{file.Id}", file);
});

app.MapDelete("/api/files/{id}", async (WopiDbContext db, string id) =>
{
    var file = await db.WopiFiles.FindAsync(id);

    if (file is null)
    {
        return Results.NotFound();
    }

    db.Remove(file);
    await db.SaveChangesAsync();

    return Results.Ok();
});

app.MapGet("/wopi/files/{id}", async (WopiDbContext db, string id) =>
{
    var file = await db.WopiFiles.FindAsync(id);

    if (file is null)
    {
        return Results.NotFound();
    }

    return Results.Json(new
    {
        BaseFileName = file.Name,
        OwnerId = "Wopi",
        Size = file.Contents.Length,
        UserId = "",
        file.Version,
        UserCanWrite = true
    }, new() { PropertyNamingPolicy = null });
});

app.MapPost("/wopi/files/{id}", async (WopiDbContext db, string id, [FromHeader(Name = "X-WOPI-Override")] string wopiOverride) =>
{
    var file = await db.WopiFiles.FindAsync(id);

    if (file is null)
    {
        return Results.NotFound();
    }

    return wopiOverride switch
    {
        // TODO implement locks
        "LOCK" or "GET_LOCK" or "REFRESH_LOCK" or "UNLOCK" => Results.Ok(),
        _ => Results.StatusCode(501)
    };
});

app.MapGet("/wopi/files/{id}/contents", async (WopiDbContext db, string id) =>
{
    var file = await db.WopiFiles.FindAsync(id);

    if (file is null)
    {
        return Results.NotFound();
    }

    return Results.File(file.Contents);
});

app.MapPost("/wopi/files/{id}/contents", async (HttpContext context, WopiDbContext db, string id) =>
{
    var file = await db.WopiFiles.FindAsync(id);

    if (file is null)
    {
        return Results.NotFound();
    }

    await using var memoryStream = new MemoryStream();
    await context.Request.Body.CopyToAsync(memoryStream);
    var contents = memoryStream.ToArray();

    file.Contents = contents;
    file.Version = Guid.NewGuid().ToString();

    await db.SaveChangesAsync();

    return Results.Ok();
});

app.Run();
