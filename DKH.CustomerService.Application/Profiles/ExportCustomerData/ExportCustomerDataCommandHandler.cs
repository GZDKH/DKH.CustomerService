using System.Text;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Services.V1;
using Google.Protobuf;
using Grpc.Core;

namespace DKH.CustomerService.Application.Profiles.ExportCustomerData;

public class ExportCustomerDataCommandHandler(ICustomerRepository repository)
    : IRequestHandler<ExportCustomerDataCommand, ExportCustomerDataResponse>
{
    public async Task<ExportCustomerDataResponse> Handle(ExportCustomerDataCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        var profileModel = profile.ToContractModel();

        return request.Format.ToLowerInvariant() switch
        {
            "json" => ExportAsJson(profileModel, request.TelegramUserId),
            "xml" => throw new RpcException(new Status(StatusCode.Unimplemented, "XML export not yet implemented")),
            "csv" => throw new RpcException(new Status(StatusCode.Unimplemented, "CSV export not yet implemented")),
            _ => throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unsupported format: {request.Format}. Supported formats: json, xml, csv"))
        };
    }

    private static ExportCustomerDataResponse ExportAsJson(Contracts.Models.V1.CustomerProfile profile, string telegramUserId)
    {
        // Convert protobuf to JSON
        var json = JsonFormatter.Default.Format(profile);
        var bytes = Encoding.UTF8.GetBytes(json);

        return new ExportCustomerDataResponse
        {
            Data = ByteString.CopyFrom(bytes),
            ContentType = "application/json",
            FileName = $"customer_{telegramUserId}_{DateTime.UtcNow:yyyyMMddHHmmss}.json"
        };
    }
}
