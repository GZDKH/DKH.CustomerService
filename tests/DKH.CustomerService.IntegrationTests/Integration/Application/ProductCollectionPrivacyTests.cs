using DKH.CustomerService.Application.ProductCollection.AddToCollection;
using DKH.CustomerService.Application.ProductCollection.GetCollection;
using DKH.CustomerService.Application.ProductCollection.GetCollectionItem;
using DKH.CustomerService.Application.ProductCollection.RemoveFromCollection;
using DKH.CustomerService.Application.ProductCollection.UpdateCollectionItem;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Identity;
using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using DomainCollectionStatus = DKH.CustomerService.Domain.Entities.ProductCollection.ProductCollectionStatus;
using DomainObservationRole = DKH.CustomerService.Domain.Entities.ProductCollection.ProductExperienceObservationRole;
using DomainObservationValueType = DKH.CustomerService.Domain.Entities.ProductCollection.ProductExperienceObservationValueType;
using ProtoCollectionStatus = DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1.ProductCollectionStatus;
using ProtoObservationRole = DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1.ProductExperienceObservationRole;

namespace DKH.CustomerService.IntegrationTests.Integration.Application;

[Trait("Category", "Integration")]
public sealed class ProductCollectionPrivacyTests
{
    [Fact]
    public async Task CollectionQueriesAndMutations_IsolateTwoCustomersAndPreserveExperienceAsync()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        await using var context = CreateContext();

        var itemA = await SeedAsync(context, customerA, productA, "A-private-note", "A-private-tag");
        var itemB = await SeedAsync(context, customerB, productB, "B-private-note", "B-private-tag");

        var customerAItems = await new GetCollectionQueryHandler(context).Handle(
            new GetCollectionQuery(customerA, null, 1, 100), CancellationToken.None);
        var customerBItems = await new GetCollectionQueryHandler(context).Handle(
            new GetCollectionQuery(customerB, null, 1, 100), CancellationToken.None);

        customerAItems.Items.Should().ContainSingle();
        customerAItems.Items[0].ProductId.Value.Should().Be(productA.ToString());
        customerAItems.Items[0].Experience.PersonalText.Should().Be("A-private-note");
        customerAItems.Items[0].Experience.Tags.Should().ContainSingle("A-private-tag");
        customerBItems.Items.Should().ContainSingle();
        customerBItems.Items[0].ProductId.Value.Should().Be(productB.ToString());
        customerBItems.Items[0].Experience.PersonalText.Should().Be("B-private-note");
        customerBItems.Items[0].Experience.Tags.Should().ContainSingle("B-private-tag");

        var guessedRead = () => new GetCollectionItemQueryHandler(context).Handle(
            new GetCollectionItemQuery(customerB, productA), CancellationToken.None);
        await guessedRead.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.NotFound);

        var guessedUpdate = () => new UpdateCollectionItemCommandHandler(context).Handle(
            new UpdateCollectionItemCommand(
                itemA.Id,
                customerB,
                null,
                "B-attempted-overwrite",
                null,
                CreateExperience("B-attempted-experience", "B-attempted-tag")),
            CancellationToken.None);
        await guessedUpdate.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.PermissionDenied);

        var guessedDelete = () => new RemoveFromCollectionCommandHandler(context).Handle(
            new RemoveFromCollectionCommand(itemA.Id, customerB), CancellationToken.None);
        await guessedDelete.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.PermissionDenied);

        context.ChangeTracker.Clear();
        var ownerRead = await new GetCollectionItemQueryHandler(context).Handle(
            new GetCollectionItemQuery(customerA, productA), CancellationToken.None);
        ownerRead.Notes.Should().Be("A-private-note");
        ownerRead.Experience.PersonalText.Should().Be("A-private-note");
        ownerRead.Experience.Tags.Should().ContainSingle("A-private-tag");
        (await context.ProductCollectionItems.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task RepeatedOwnerUpsert_ReplacesOneExperienceAndExplicitEmptyClearsItAsync()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await using var context = CreateContext();

        var first = await new AddToCollectionCommandHandler(context).Handle(
            new AddToCollectionCommand(
                customerId,
                productId,
                null,
                ProtoCollectionStatus.Own,
                "note",
                4,
                CreateExperience("first", "first-tag")),
            CancellationToken.None);

        var second = await new AddToCollectionCommandHandler(context).Handle(
            new AddToCollectionCommand(
                customerId,
                productId,
                null,
                ProtoCollectionStatus.Using,
                null,
                null,
                CreateExperience("second", "second-tag")),
            CancellationToken.None);

        second.Id.Value.Should().Be(first.Id.Value);
        second.Status.Should().Be(ProtoCollectionStatus.Using);
        second.Notes.Should().Be("note");
        second.Rating.Should().Be(4);
        second.Experience.PersonalText.Should().Be("second");
        second.Experience.Tags.Should().ContainSingle("second-tag");
        (await context.ProductCollectionItems.CountAsync()).Should().Be(1);
        (await context.ProductExperiences.CountAsync()).Should().Be(1);

        var cleared = await new UpdateCollectionItemCommandHandler(context).Handle(
            new UpdateCollectionItemCommand(
                Guid.Parse(first.Id.Value),
                customerId,
                null,
                null,
                null,
                new ProductExperienceModel()),
            CancellationToken.None);

        cleared.Experience.Should().BeNull();
        cleared.Rating.Should().Be(4);
        (await context.ProductExperiences.CountAsync()).Should().Be(0);
    }

    private static async Task<ProductCollectionItemEntity> SeedAsync(
        AppDbContext context,
        Guid customerId,
        Guid productId,
        string note,
        string tag)
    {
        var item = ProductCollectionItemEntity.Create(
            customerId,
            productId,
            status: DomainCollectionStatus.Own,
            notes: note,
            rating: 3);
        var experience = ProductExperienceEntity.Create(
            item.Id,
            DateTimeOffset.UtcNow,
            note,
            $"recommendation-{tag}",
            [(
                Guid.NewGuid(),
                DomainObservationRole.Observation,
                DomainObservationValueType.Text,
                $"aroma-{tag}",
                null,
                null,
                null,
                null)],
            [tag]);
        item.ReplaceExperience(experience);
        context.ProductCollectionItems.Add(item);
        context.ProductExperiences.Add(experience);
        await context.SaveChangesAsync();
        return item;
    }

    private static ProductExperienceModel CreateExperience(string text, string tag)
    {
        var model = new ProductExperienceModel
        {
            PersonalText = text,
            Recommendation = $"recommendation-{tag}",
        };
        model.Tags.Add(tag);
        model.Observations.Add(new ProductExperienceObservationModel
        {
            DefinitionId = GuidValue.FromGuid(Guid.NewGuid()),
            Role = ProtoObservationRole.Observation,
            TextValue = $"aroma-{tag}",
        });
        return model;
    }

    private static AppDbContext CreateContext()
    {
        var serviceProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddSingleton(Substitute.For<IPlatformCurrentUser>())
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseInternalServiceProvider(serviceProvider)
            .Options;
        return new AppDbContext(options);
    }
}
