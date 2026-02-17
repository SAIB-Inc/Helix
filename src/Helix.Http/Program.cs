using Helix.Core.Extensions;
using Helix.Tools.Users;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("HELIX__");

builder.Services.AddHelixCore(builder.Configuration);
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(UserTools).Assembly);

var app = builder.Build();

app.MapMcp();

app.Run();
