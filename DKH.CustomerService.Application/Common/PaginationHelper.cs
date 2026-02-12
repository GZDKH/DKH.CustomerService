using DKH.Platform.Grpc.Common.Types;

namespace DKH.CustomerService.Application.Common;

public static class PaginationHelper
{
    public static PaginationMetadata CreateMetadata(int totalCount, int page, int pageSize)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PaginationMetadata
        {
            TotalCount = totalCount,
            TotalPages = totalPages,
            CurrentPage = page,
            PageSize = pageSize,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1,
        };
    }
}
