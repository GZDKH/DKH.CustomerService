using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using DKH.Platform.Grpc.Common.Types;
using FluentAssertions;
using Grpc.Core;
using Xunit;
using DomainValueType = DKH.CustomerService.Domain.Entities.ProductCollection.ProductExperienceObservationValueType;
using ProtoRole = DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1.ProductExperienceObservationRole;

namespace DKH.CustomerService.Application.Tests;

public sealed class ProductExperienceTests
{
    [Fact]
    public void ToDomain_PreservesZeroAndFalseAsPresentValues()
    {
        var experience = new ProductExperienceModel();
        experience.Observations.Add(new ProductExperienceObservationModel
        {
            DefinitionId = GuidValue.FromGuid(Guid.NewGuid()),
            Role = ProtoRole.Observation,
            DecimalValue = 0,
        });
        experience.Observations.Add(new ProductExperienceObservationModel
        {
            DefinitionId = GuidValue.FromGuid(Guid.NewGuid()),
            Role = ProtoRole.RecommendedPreparation,
            BooleanValue = false,
        });

        var result = ProductExperienceMapper.ToDomain(experience, Guid.NewGuid());

        result.Should().NotBeNull();
        result!.Observations.Should().HaveCount(2);
        result.Observations.Should().ContainSingle(x => x.ValueType == DomainValueType.Decimal && x.DecimalValue == 0);
        result.Observations.Should().ContainSingle(x => x.ValueType == DomainValueType.Boolean && x.BooleanValue == false);
    }

    [Fact]
    public void ToDomain_EmptyMessageMeansClear()
    {
        ProductExperienceMapper.ToDomain(new ProductExperienceModel(), Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void ToDomain_RejectsDuplicateDefinitionAndRole()
    {
        var definitionId = Guid.NewGuid();
        var experience = new ProductExperienceModel();
        experience.Observations.Add(CreateTextObservation(definitionId, "first"));
        experience.Observations.Add(CreateTextObservation(definitionId, "second"));

        var action = () => ProductExperienceMapper.ToDomain(experience, Guid.NewGuid());

        action.Should().Throw<RpcException>()
            .Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public void ToDomain_RejectsObservationWithoutTypedValue()
    {
        var experience = new ProductExperienceModel();
        experience.Observations.Add(new ProductExperienceObservationModel
        {
            DefinitionId = GuidValue.FromGuid(Guid.NewGuid()),
            Role = ProtoRole.Observation,
        });

        var action = () => ProductExperienceMapper.ToDomain(experience, Guid.NewGuid());

        action.Should().Throw<RpcException>()
            .Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    private static ProductExperienceObservationModel CreateTextObservation(Guid definitionId, string value)
        => new()
        {
            DefinitionId = GuidValue.FromGuid(definitionId),
            Role = ProtoRole.Observation,
            TextValue = value,
        };
}
