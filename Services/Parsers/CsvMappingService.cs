// File: Services/Parsers/CsvMappingService.cs
using CsvHelper;
using System.Collections.Generic;

namespace EINVWORLD.Services.Parsers
{
    public class CsvMappingService
    {
        public List<string> DetectCsvHeaders(CsvReader csv)
        {
            csv.Read();
            csv.ReadHeader();
            return (csv.HeaderRecord ?? Array.Empty<string>()).ToList();
        }
    }
}