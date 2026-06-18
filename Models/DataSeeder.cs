using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using eInvWorld.Models;
using eInvWorld.Data;

public class DataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(ApplicationDbContext context, IConfiguration configuration, ILogger<DataSeeder> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedDataAsync()
    {
        try
        {
            _logger.LogInformation("Starting data seeding process.");

            await SeedStateCodesAsync();
            await SeedCountryCodesAsync();
            await SeedCurrencyCodesAsync();
            await SeedClassificationCodesAsync();
            await SeedEInvoiceTypesAsync();
            await SeedMSICSubCategoryCodesAsync();
            await SeedPaymentModesAsync();
            await SeedTaxTypesAsync();
            await SeedUnitTypesAsync();

            _logger.LogInformation("Data seeding process completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during the data seeding process.");
            throw;
        }
    }

    private async Task SeedStateCodesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:StateCodes"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding state codes from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var stateCodes = JsonConvert.DeserializeObject<List<StateCode>>(jsonData) ?? new List<StateCode>();

        foreach (var stateCode in stateCodes)
        {
            var existingStateCode = await _context.StateCodes
                .FirstOrDefaultAsync(sc => sc.Code == stateCode.Code);

            if (existingStateCode == null)
            {
                stateCode.IsActive = true;
                stateCode.UpdatedBy = "System Seeder";
                stateCode.UpdatedDate = DateTime.UtcNow;

                await _context.StateCodes.AddAsync(stateCode);
                _logger.LogInformation($"Added state code: {stateCode.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingStateCode.UpdatedBy))
                {
                    existingStateCode.UpdatedBy = "System Seeder";
                    existingStateCode.UpdatedDate = DateTime.UtcNow;

                    _context.StateCodes.Update(existingStateCode);
                    _logger.LogInformation($"Updated existing state code audit fields: {existingStateCode.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedCountryCodesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:CountryCodes"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding country codes from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var countryCodes = JsonConvert.DeserializeObject<List<CountryCode>>(jsonData) ?? new List<CountryCode>();

        foreach (var countryCode in countryCodes)
        {
            var existingCountryCode = await _context.CountryCodes
                .FirstOrDefaultAsync(cc => cc.Code == countryCode.Code);

            if (existingCountryCode == null)
            {
                countryCode.IsActive = true;
                countryCode.UpdatedBy = "System Seeder";
                countryCode.UpdatedDate = DateTime.UtcNow;

                await _context.CountryCodes.AddAsync(countryCode);
                _logger.LogInformation($"Added country code: {countryCode.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingCountryCode.UpdatedBy))
                {
                    existingCountryCode.UpdatedBy = "System Seeder";
                    existingCountryCode.UpdatedDate = DateTime.UtcNow;

                    _context.CountryCodes.Update(existingCountryCode);
                    _logger.LogInformation($"Updated existing country code audit fields: {existingCountryCode.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedCurrencyCodesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:CurrencyCodes"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding currency codes from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var currencyCodes = JsonConvert.DeserializeObject<List<CurrencyCode>>(jsonData) ?? new List<CurrencyCode>();

        foreach (var currencyCode in currencyCodes)
        {
            var existingCurrencyCode = await _context.CurrencyCodes
                .FirstOrDefaultAsync(cc => cc.Code == currencyCode.Code);

            if (existingCurrencyCode == null)
            {
                currencyCode.IsActive = true;
                currencyCode.UpdatedBy = "System Seeder";
                currencyCode.UpdatedDate = DateTime.UtcNow;

                await _context.CurrencyCodes.AddAsync(currencyCode);
                _logger.LogInformation($"Added currency code: {currencyCode.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingCurrencyCode.UpdatedBy))
                {
                    existingCurrencyCode.UpdatedBy = "System Seeder";
                    existingCurrencyCode.UpdatedDate = DateTime.UtcNow;

                    _context.CurrencyCodes.Update(existingCurrencyCode);
                    _logger.LogInformation($"Updated existing currency code audit fields: {existingCurrencyCode.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedClassificationCodesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:ClassificationCodes"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding classification codes from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var classificationCodes = JsonConvert.DeserializeObject<List<ClassificationCode>>(jsonData) ?? new List<ClassificationCode>();

        foreach (var classificationCode in classificationCodes)
        {
            var existingClassificationCode = await _context.ClassificationCodes
                .FirstOrDefaultAsync(cc => cc.Code == classificationCode.Code);

            if (existingClassificationCode == null)
            {
                classificationCode.IsActive = true;
                classificationCode.UpdatedBy = "System Seeder";
                classificationCode.UpdatedDate = DateTime.UtcNow;

                await _context.ClassificationCodes.AddAsync(classificationCode);
                _logger.LogInformation($"Added classification code: {classificationCode.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingClassificationCode.UpdatedBy))
                {
                    existingClassificationCode.UpdatedBy = "System Seeder";
                    existingClassificationCode.UpdatedDate = DateTime.UtcNow;

                    _context.ClassificationCodes.Update(existingClassificationCode);
                    _logger.LogInformation($"Updated existing classification code audit fields: {existingClassificationCode.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedEInvoiceTypesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:EInvoiceTypes"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding e-invoice types from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var eInvoiceTypes = JsonConvert.DeserializeObject<List<EInvoiceType>>(jsonData) ?? new List<EInvoiceType>();

        foreach (var eInvoiceType in eInvoiceTypes)
        {
            var existingEInvoiceType = await _context.EInvoiceTypes
                .FirstOrDefaultAsync(eit => eit.Code == eInvoiceType.Code);

            if (existingEInvoiceType == null)
            {
                eInvoiceType.IsActive = true;
                eInvoiceType.UpdatedBy = "System Seeder";
                eInvoiceType.UpdatedDate = DateTime.UtcNow;

                await _context.EInvoiceTypes.AddAsync(eInvoiceType);
                _logger.LogInformation($"Added e-invoice type: {eInvoiceType.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingEInvoiceType.UpdatedBy))
                {
                    existingEInvoiceType.UpdatedBy = "System Seeder";
                    existingEInvoiceType.UpdatedDate = DateTime.UtcNow;

                    _context.EInvoiceTypes.Update(existingEInvoiceType);
                    _logger.LogInformation($"Updated existing e-invoice type audit fields: {existingEInvoiceType.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedMSICSubCategoryCodesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:MSICSubCategoryCodes"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding MSIC sub-category codes from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var msicSubCategoryCodes = JsonConvert.DeserializeObject<List<MSICSubCategoryCode>>(jsonData) ?? new List<MSICSubCategoryCode>();

        foreach (var msicSubCategoryCode in msicSubCategoryCodes)
        {
            var existingMSICSubCategoryCode = await _context.MSICSubCategoryCodes
                .FirstOrDefaultAsync(msc => msc.Code == msicSubCategoryCode.Code);

            if (existingMSICSubCategoryCode == null)
            {
                msicSubCategoryCode.IsActive = true;
                msicSubCategoryCode.UpdatedBy = "System Seeder";
                msicSubCategoryCode.UpdatedDate = DateTime.UtcNow;

                await _context.MSICSubCategoryCodes.AddAsync(msicSubCategoryCode);
                _logger.LogInformation($"Added MSIC sub-category code: {msicSubCategoryCode.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingMSICSubCategoryCode.UpdatedBy))
                {
                    existingMSICSubCategoryCode.UpdatedBy = "System Seeder";
                    existingMSICSubCategoryCode.UpdatedDate = DateTime.UtcNow;

                    _context.MSICSubCategoryCodes.Update(existingMSICSubCategoryCode);
                    _logger.LogInformation($"Updated existing MSIC sub-category code audit fields: {existingMSICSubCategoryCode.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedPaymentModesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:PaymentMethods"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding payment modes from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var paymentModes = JsonConvert.DeserializeObject<List<PaymentMode>>(jsonData) ?? new List<PaymentMode>();

        foreach (var paymentMode in paymentModes)
        {
            var existingPaymentMode = await _context.PaymentMethods
                .FirstOrDefaultAsync(pm => pm.Code == paymentMode.Code);

            if (existingPaymentMode == null)
            {
                paymentMode.IsActive = true;
                paymentMode.UpdatedBy = "System Seeder";
                paymentMode.UpdatedDate = DateTime.UtcNow;

                await _context.PaymentMethods.AddAsync(paymentMode);
                _logger.LogInformation($"Added payment mode: {paymentMode.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingPaymentMode.UpdatedBy))
                {
                    existingPaymentMode.UpdatedBy = "System Seeder";
                    existingPaymentMode.UpdatedDate = DateTime.UtcNow;

                    _context.PaymentMethods.Update(existingPaymentMode);
                    _logger.LogInformation($"Updated existing payment mode audit fields: {existingPaymentMode.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedTaxTypesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:TaxTypes"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding tax types from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var taxTypes = JsonConvert.DeserializeObject<List<TaxType>>(jsonData) ?? new List<TaxType>();

        foreach (var taxType in taxTypes)
        {
            var existingTaxType = await _context.TaxTypes
                .FirstOrDefaultAsync(tt => tt.Code == taxType.Code);

            if (existingTaxType == null)
            {
                taxType.IsActive = true;
                taxType.UpdatedBy = "System Seeder";
                taxType.UpdatedDate = DateTime.UtcNow;

                await _context.TaxTypes.AddAsync(taxType);
                _logger.LogInformation($"Added tax type: {taxType.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingTaxType.UpdatedBy))
                {
                    existingTaxType.UpdatedBy = "System Seeder";
                    existingTaxType.UpdatedDate = DateTime.UtcNow;

                    _context.TaxTypes.Update(existingTaxType);
                    _logger.LogInformation($"Updated existing tax type audit fields: {existingTaxType.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedUnitTypesAsync()
    {
        var relativePath = _configuration["CodeFilePaths:UnitTypes"];
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath ?? string.Empty);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation($"Seeding unit types from file: {filePath}");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var unitTypes = JsonConvert.DeserializeObject<List<UnitType>>(jsonData) ?? new List<UnitType>();

        foreach (var unitType in unitTypes)
        {
            var existingUnitType = await _context.UnitTypes
                .FirstOrDefaultAsync(ut => ut.Code == unitType.Code);

            if (existingUnitType == null)
            {
                unitType.IsActive = true;
                unitType.UpdatedBy = "System Seeder";
                unitType.UpdatedDate = DateTime.UtcNow;

                await _context.UnitTypes.AddAsync(unitType);
                _logger.LogInformation($"Added unit type: {unitType.Code}");
            }
            else
            {
                if (string.IsNullOrEmpty(existingUnitType.UpdatedBy))
                {
                    existingUnitType.UpdatedBy = "System Seeder";
                    existingUnitType.UpdatedDate = DateTime.UtcNow;

                    _context.UnitTypes.Update(existingUnitType);
                    _logger.LogInformation($"Updated existing unit type audit fields: {existingUnitType.Code}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}