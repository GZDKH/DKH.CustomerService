using System.Globalization;
using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;
using DKH.CustomerService.Contracts.Customer.Models.AccountStatus.v1;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace DKH.CustomerService.Application.Admin.GetCustomerStats;

public class GetCustomerStatsQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetCustomerStatsQuery, GetCustomerStatsResponse>
{
    public async Task<GetCustomerStatsResponse> Handle(GetCustomerStatsQuery request, CancellationToken cancellationToken)
    {
        var storefrontId = request.StorefrontId
                           ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required for stats"));

        var profile = await repository.GetByUserIdWithAllRelationsAsync(
            storefrontId,
            request.UserId,
            cancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound,
                $"Customer with userId '{request.UserId}' not found."));

        var stats = new CustomerStatsModel
        {
            CustomerId = GuidValue.FromGuid(profile.Id),
            TotalOrdersCount = profile.AccountStatus.TotalOrdersCount,
            TotalSpent = profile.AccountStatus.TotalSpent.ToString("F2", CultureInfo.InvariantCulture),
            WishlistItemsCount = profile.WishlistItems.Count(w => !w.IsDeleted),
            AddressesCount = profile.Addresses.Count(a => !a.IsDeleted),
            RegisteredAt = Timestamp.FromDateTime(DateTime.SpecifyKind(profile.CreationTime, DateTimeKind.Utc)),
        };

        if (profile.AccountStatus.LastActivityAt.HasValue)
        {
            stats.LastActivityAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(profile.AccountStatus.LastActivityAt.Value, DateTimeKind.Utc));
        }

        return new GetCustomerStatsResponse { Stats = stats };
    }
}
