using DKH.Platform.DataExchange.Options;

namespace DKH.CustomerService.Application.CustomerProfiles.DataExchange;

public sealed class CustomerDataExchangeProfileProvider(IOptions<PlatformLocalizationOptions> localizationOptions)
    : IPlatformImportProfileProviderWithFormat,
        IPlatformExportProfileProviderWithFormat,
        IPlatformConfigurePlatformDataExchangeOptions,
        IPlatformHasProfileName
{
    public const string Customers = "customers";

    public void Configure(PlatformDataExchangeOptions options)
        => options.UseSchema(CustomerDataExchangeSchema.Schema, Customers);

    PlatformExportProfile IPlatformExportProfileProvider.GetProfile(string name)
    {
        if (string.Equals(name, Customers, StringComparison.OrdinalIgnoreCase))
        {
            return CreateExport(localizationOptions.Value);
        }

        throw new InvalidOperationException($"Export profile '{name}' is not registered.");
    }

    PlatformExportProfile IPlatformExportProfileProviderWithFormat.GetProfile(string name, PlatformFileFormat format)
    {
        if (!string.Equals(name, Customers, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Export profile '{name}' is not registered.");
        }

        return CreateExport(localizationOptions.Value, format);
    }

    public string ProfileName => Customers;

    public PlatformImportProfile GetProfile(string name)
    {
        if (string.Equals(name, Customers, StringComparison.OrdinalIgnoreCase))
        {
            return CreateImport(localizationOptions.Value);
        }

        throw new InvalidOperationException($"Import profile '{name}' is not registered.");
    }

    PlatformImportProfile IPlatformImportProfileProviderWithFormat.GetProfile(string name, PlatformFileFormat format)
    {
        if (!string.Equals(name, Customers, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Import profile '{name}' is not registered.");
        }

        return CreateImport(localizationOptions.Value, format);
    }

    private static PlatformImportProfile CreateImport(PlatformLocalizationOptions options, PlatformFileFormat? format = null)
    {
        var cultures = PlatformLocalizationColumnHelper.GetCultures(options);
        return CustomerDataExchangeSchema.Schema.ToImportProfile(
            Customers,
            cultures,
            format,
            [nameof(CustomerDataExchangeDto.UserId)],
            []);
    }

    private static PlatformExportProfile CreateExport(PlatformLocalizationOptions options, PlatformFileFormat? format = null)
    {
        var cultures = PlatformLocalizationColumnHelper.GetCultures(options);
        return CustomerDataExchangeSchema.Schema.ToExportProfile(Customers, cultures, format);
    }
}
