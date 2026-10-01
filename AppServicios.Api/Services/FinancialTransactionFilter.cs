using AppServicios.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace AppServicios.Api.Services;

// Transaction-scoped PostgreSQL lock works across processes and replicas. Every financial
// controller enters it BEFORE loading balances. SaveChanges and audit entries commit together.
public sealed class FinancialTransactionFilter(AppServiciosDbContext db, ILogger<FinancialTransactionFilter> logger, SecurityAlerts alerts) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'");
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7342198601)");
            var result = await next();
            if (result.Exception is not null && !result.ExceptionHandled)
            {
                if (result.Exception is PaymentCheckException error)
                {
                    logger.LogWarning("Payment security rejected {Path}: {Reason}", context.HttpContext.Request.Path, error.Message);
                    result.Result = new ObjectResult(new { message = error.Message }) { StatusCode = error.Status };
                    result.ExceptionHandled = true;
                    await tx.RollbackAsync();
                    await alerts.Record("Control de pagos", error.Message);
                }
                return;
            }
            if (((result.Result as IStatusCodeActionResult)?.StatusCode ?? 200) < 400) await tx.CommitAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "55P03")
        {
            context.Result = new ConflictObjectResult(new { message = "Hay otra operación en curso. Reintentá en unos segundos." });
        }
    }
}
