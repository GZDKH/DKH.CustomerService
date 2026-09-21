using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using DomainRole = DKH.CustomerService.Domain.Entities.ProductCollection.ProductExperienceObservationRole;
using DomainValueType = DKH.CustomerService.Domain.Entities.ProductCollection.ProductExperienceObservationValueType;
using ProtoRole = DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1.ProductExperienceObservationRole;

namespace DKH.CustomerService.Application.Mappers;

public static class ProductExperienceMapper
{
    public static ProductExperienceEntity? ToDomain(ProductExperienceModel? model, Guid collectionItemId)
    {
        if (model is null)
        {
            return null;
        }

        if (model.ExperiencedAt is null
            && model.PersonalText is null
            && model.Recommendation is null
            && model.Tags.Count == 0
            && model.Observations.Count == 0)
        {
            return null;
        }

        try
        {
            var observations = model.Observations.Select(ToDomainObservation).ToArray();
            var tags = model.Tags.ToArray();
            return ProductExperienceEntity.Create(
                collectionItemId,
                model.ExperiencedAt?.ToDateTimeOffset(),
                model.PersonalText,
                model.Recommendation,
                observations,
                tags);
        }
        catch (ArgumentException exception)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message));
        }
    }

    public static ProductExperienceModel? ToProto(ProductExperienceEntity? entity)
    {
        if (entity is null)
        {
            return null;
        }

        var model = new ProductExperienceModel();
        if (entity.ExperiencedAt.HasValue)
        {
            model.ExperiencedAt = Timestamp.FromDateTimeOffset(entity.ExperiencedAt.Value);
        }

        if (entity.PersonalText is not null)
        {
            model.PersonalText = entity.PersonalText;
        }

        if (entity.Recommendation is not null)
        {
            model.Recommendation = entity.Recommendation;
        }

        model.Tags.Add(entity.Tags.OrderBy(t => t.Value).Select(t => t.Value));
        model.Observations.Add(entity.Observations
            .OrderBy(o => o.Role)
            .ThenBy(o => o.DefinitionId)
            .Select(ToProtoObservation));
        return model;
    }

    private static (Guid DefinitionId, DomainRole Role, DomainValueType ValueType, string? TextValue, double? DecimalValue, long? IntegerValue, bool? BooleanValue, string? UnitCode) ToDomainObservation(ProductExperienceObservationModel model)
    {
        var definitionId = model.DefinitionId?.ToGuid() ?? Guid.Empty;
        var role = model.Role switch
        {
            ProtoRole.Observation => DomainRole.Observation,
            ProtoRole.RecommendedPreparation => DomainRole.RecommendedPreparation,
            _ => throw new ArgumentException("Observation role is required.", nameof(model)),
        };

        return model.TypedValueCase switch
        {
            ProductExperienceObservationModel.TypedValueOneofCase.TextValue =>
                (definitionId, role, DomainValueType.Text, model.TextValue, null, null, null, model.UnitCode),
            ProductExperienceObservationModel.TypedValueOneofCase.DecimalValue =>
                (definitionId, role, DomainValueType.Decimal, null, model.DecimalValue, null, null, model.UnitCode),
            ProductExperienceObservationModel.TypedValueOneofCase.IntegerValue =>
                (definitionId, role, DomainValueType.Integer, null, null, model.IntegerValue, null, model.UnitCode),
            ProductExperienceObservationModel.TypedValueOneofCase.BooleanValue =>
                (definitionId, role, DomainValueType.Boolean, null, null, null, model.BooleanValue, model.UnitCode),
            _ => throw new ArgumentException("Exactly one observation value is required.", nameof(model)),
        };
    }

    private static ProductExperienceObservationModel ToProtoObservation(ProductExperienceObservationEntity entity)
    {
        var model = new ProductExperienceObservationModel
        {
            DefinitionId = GuidValue.FromGuid(entity.DefinitionId),
            Role = entity.Role switch
            {
                DomainRole.Observation => ProtoRole.Observation,
                DomainRole.RecommendedPreparation => ProtoRole.RecommendedPreparation,
                _ => ProtoRole.Unspecified,
            },
            UnitCode = entity.UnitCode ?? string.Empty,
        };

        switch (entity.ValueType)
        {
            case DomainValueType.Text:
                model.TextValue = entity.TextValue ?? string.Empty;
                break;
            case DomainValueType.Decimal:
                model.DecimalValue = entity.DecimalValue ?? 0;
                break;
            case DomainValueType.Integer:
                model.IntegerValue = entity.IntegerValue ?? 0;
                break;
            case DomainValueType.Boolean:
                model.BooleanValue = entity.BooleanValue ?? false;
                break;
        }

        return model;
    }
}
