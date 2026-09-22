using ClosedXML.Excel;
using System.Reflection;
using System.Text;

namespace cpms_Application.Utilities
{
    /// <summary>
    /// Shared ClosedXML helpers for role-specific exports. Rows are plain record
    /// types; every public instance property becomes a column.
    /// </summary>
    public static class ExcelSheetWriter
    {
        public static void AddSheet<T>(XLWorkbook workbook, string sheetName, IReadOnlyCollection<T> rows)
        {
            var worksheet = workbook.Worksheets.Add(sheetName);
            var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

            for (var column = 0; column < properties.Length; column++)
            {
                var cell = worksheet.Cell(1, column + 1);
                cell.Value = ToHeader(properties[column].Name);
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F1FF");
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            var rowNumber = 2;
            foreach (var row in rows)
            {
                for (var column = 0; column < properties.Length; column++)
                {
                    var value = properties[column].GetValue(row);
                    worksheet.Cell(rowNumber, column + 1).Value = ToExcelValue(value);
                }

                rowNumber++;
            }

            if (properties.Length > 0)
            {
                var range = worksheet.Range(1, 1, Math.Max(rowNumber - 1, 1), properties.Length);
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                range.Style.Alignment.WrapText = true;
                worksheet.SheetView.FreezeRows(1);
                worksheet.Columns(1, properties.Length).AdjustToContents();
            }
        }

        public static XLCellValue ToExcelValue(object? value)
        {
            if (value == null)
                return string.Empty;
            if (value is string stringValue)
                return stringValue;
            if (value is IEnumerable<string> stringValues)
                return string.Join("; ", stringValues.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (value is int intValue)
                return intValue;
            if (value is decimal decimalValue)
                return decimalValue;
            if (value is double doubleValue)
                return doubleValue;
            if (value is bool boolValue)
                return boolValue;
            if (value is DateTime dateTimeValue)
                return dateTimeValue;

            return value.ToString() ?? string.Empty;
        }

        public static string ToHeader(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append(propertyName[0]);
            for (var i = 1; i < propertyName.Length; i++)
            {
                if (char.IsUpper(propertyName[i]) && !char.IsWhiteSpace(propertyName[i - 1]))
                    builder.Append(' ');
                builder.Append(propertyName[i]);
            }

            return builder.ToString();
        }

        public static string BuildDownloadFileName(string? requestedName, string fallback)
        {
            var baseName = string.IsNullOrWhiteSpace(requestedName) ? fallback : requestedName;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = fallback;

            foreach (var invalid in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(invalid, '-');

            baseName = baseName.Trim();
            if (baseName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                baseName = baseName[..^5];
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = fallback;

            return $"{baseName}-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        }
    }
}
