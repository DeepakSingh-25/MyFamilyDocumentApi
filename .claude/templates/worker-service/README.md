# Worker Service Template

## When to Use

Use this template when you're building:
- A background worker that processes messages from RabbitMQ or Azure Service Bus
- A hosted service that performs periodic or scheduled work
- A long-running process that reacts to events (Wolverine or MassTransit consumers)
- A job processing service using Hangfire or similar
- A headless service with no HTTP endpoints (aside from optional health checks)

## How to Use

1. Copy `CLAUDE.md` into the root of your .NET Worker Service project
2. Replace `[ProjectName]` with your actual project name
3. Update the **Tech Stack** section to match your dependencies
4. Remove optional items (Hangfire, EF Core) from the tech stack if not used
5. Remove any skills references that don't apply to your project

## What's Included

This template configures Claude Code to:
- Structure workers with proper `BackgroundService` inheritance and cancellation
- Use Wolverine or MassTransit for message consumption with correctly scoped consumers
- Create service scopes inside long-running loops to avoid captive dependency issues
- Follow structured logging with Serilog
- Write tests with xUnit v3 and Testcontainers
- Avoid common async pitfalls (`async void`, `Thread.Sleep`, fire-and-forget tasks)
- Use .NET 10 / C# 14 modern patterns

## Customization

### Message Broker Only (No Scheduled Jobs)

Remove the `Jobs/` folder from the architecture section and remove the Hangfire reference from the tech stack. Focus on the `Consumers/` folder for Wolverine/MassTransit consumers.

### Scheduled Jobs Only (No Message Broker)

Remove the `Consumers/` folder from the architecture section and remove Wolverine/MassTransit from the tech stack. Use the `Workers/` folder for `BackgroundService` implementations with `Task.Delay`-based loops, or add Hangfire for cron-scheduled recurring jobs.

### Adding Health Checks

If your worker runs in Kubernetes or behind a load balancer, add a minimal HTTP endpoint for health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddRabbitMQ()
    .AddDbContextCheck<AppDbContext>();

var app = builder.Build();
app.MapHealthChecks("/healthz");
```

Update the tech stack to mention health check endpoints.

### Adding Aspire Integration

Add the `aspire` skill reference and configure the worker as a project resource in the Aspire AppHost.
