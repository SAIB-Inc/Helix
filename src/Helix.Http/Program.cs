using dotenv.net;
using Helix.Core.Extensions;
using Helix.Tools.Users;

DotEnv.Load();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHelixCore(builder.Configuration);
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(UserTools).Assembly);

WebApplication app = builder.Build();

app.MapMcp();

app.Run();
