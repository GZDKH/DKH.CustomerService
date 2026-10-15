using DKH.CustomerService.Infrastructure.Persistence.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

// CPU-only compiled migration metadata checks. Native tests independently verify
// the real PostgreSQL no-loss behavior; this class starts no database or host.
[Trait("Category", "Unit")]
public sealed class DeliveryContactMigrationMetadataTests
{
    [Fact]
    public void DowngradeLocksEveryRetainedTableBeforeEvaluatingEmptinessAndKeepsTransaction()
    {
        var operation = new AddDeliveryContactAuthority().DownOperations[0].Should().BeOfType<SqlOperation>().Subject;
        var predicate = operation.Sql.IndexOf("IF EXISTS", StringComparison.Ordinal);
        predicate.Should().BeGreaterThan(0);
        var beforePredicate = operation.Sql[..predicate];
        beforePredicate.Should().Contain("LOCK TABLE").And.Contain("IN ACCESS EXCLUSIVE MODE");
        foreach (var table in new[] { "delivery_contact_revisions", "delivery_contact_consents", "delivery_contact_leases" })
        {
            beforePredicate.Should().Contain("public." + table);
        }

        operation.SuppressTransaction.Should().BeFalse("the same migration transaction must retain the locks through destructive DDL");
    }

    [Fact]
    public void EveryRetainedTableHasAStatementTruncateGuardAsWellAsRowGuards()
    {
        var sql = string.Join("\n", new AddDeliveryContactAuthority().UpOperations.OfType<SqlOperation>().Select(operation => operation.Sql));
        foreach (var table in new[] { "delivery_contact_revisions", "delivery_contact_consents", "delivery_contact_leases" })
        {
            sql.Should().Contain("BEFORE TRUNCATE ON public." + table);
            sql.Should().Contain("BEFORE UPDATE OR DELETE ON public." + table);
        }

        System.Text.RegularExpressions.Regex.Count(sql, "FOR EACH STATEMENT EXECUTE FUNCTION public.customer_delivery_contact_immutable\\(\\)").Should().Be(3);
    }
}
