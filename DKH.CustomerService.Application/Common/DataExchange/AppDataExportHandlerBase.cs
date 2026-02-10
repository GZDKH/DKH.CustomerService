using System.Runtime.CompilerServices;

namespace DKH.CustomerService.Application.Common.DataExchange;

/// <summary>
///     Base class for all CustomerService export handlers.
///     Provides common infrastructure for DTO-based export with localization.
/// </summary>
/// <typeparam name="TEntity">The domain entity type.</typeparam>
/// <typeparam name="TDto">The DTO type used for export.</typeparam>
public abstract class AppDataExportHandlerBase<TEntity, TDto>(
    IAppDbContext dbContext,
    IOptions<PlatformLocalizationOptions> localizationOptions)
    : PlatformDtoBasedExportHandler<TEntity, TDto>, IPlatformHasProfileName
    where TEntity : class
    where TDto : class
{
    /// <summary>
    ///     Gets the database context.
    /// </summary>
    protected IAppDbContext DbContext => dbContext;

    /// <summary>
    ///     Gets the list of supported cultures from localization options.
    /// </summary>
    protected override IReadOnlyList<string> Cultures
        => PlatformLocalizationColumnHelper.GetCultures(localizationOptions.Value);

    /// <summary>
    ///     Gets the profile name for this export handler.
    /// </summary>
    public abstract string ProfileName { get; }

    /// <summary>
    ///     Gets entities for export by building and executing the query.
    /// </summary>
    /// <param name="context">The export context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of entities to export.</returns>
    protected sealed override async IAsyncEnumerable<TEntity> GetEntitiesAsync(
        PlatformExportContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = BuildQuery(context);
        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity;
        }
    }

    /// <summary>
    ///     Builds the query for export.
    ///     Implement filtering, searching, sorting, and includes here.
    /// </summary>
    /// <param name="context">The export context containing search, paging, and custom parameters.</param>
    /// <returns>The configured query.</returns>
    protected abstract IQueryable<TEntity> BuildQuery(PlatformExportContext context);

    /// <summary>
    ///     Applies pagination to the query based on the export context.
    /// </summary>
    /// <typeparam name="T">The query element type.</typeparam>
    /// <param name="query">The query to paginate.</param>
    /// <param name="context">The export context with paging info.</param>
    /// <returns>The paginated query.</returns>
    protected static IQueryable<T> ApplyPaging<T>(IQueryable<T> query, PlatformExportContext context)
    {
        var paging = context.GetPaging();
        if (paging is not null)
        {
            query = query.Skip(paging.Value.Skip).Take(paging.Value.Take);
        }

        return query;
    }
}
