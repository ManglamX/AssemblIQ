using System.Globalization;
using System.Text.Json;

using ClosedXML.Excel;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;

namespace SmartTorque.Services;

/// <summary>
/// Settings keys the importer reads (Architecture §7.3).
/// </summary>
public static class BmSettings
{
    public const string HeaderMapKey = "BmHeaderMap";
}

/// <summary>
/// Column headings to look for, one per BM field (PRD BM-2). Stored as JSON in the
/// settings table so the Admin can match whatever headings the supplier's sheet uses.
/// Matching is trimmed and case-insensitive (Architecture §7.3 step 2).
/// </summary>
public record BmHeaderMap
{
    public string ProductCode { get; init; } = "ProductCode";
    public string Quantity { get; init; } = "Quantity";
    public string DueDate { get; init; } = "DueDate";
    public string Remarks { get; init; } = "Remarks";
    public string SalesOrder { get; init; } = "Sales Order";
    public string PONo { get; init; } = "PONo";

    public static BmHeaderMap Default { get; } = new();
}

/// <summary>One workbook row, read and validated but not yet persisted.</summary>
public record ParsedBmRow(
    int ExcelRowNo,
    string ProductCode,
    int? Quantity,
    DateOnly? DueDate,
    string? Remarks,
    string? ColourHex,
    PriorityLevel Priority,
    BmRowImportStatus Status,
    string? Message,
    string? SalesOrder = null,
    string? PONo = null)
{
    public bool IsOk => Status == BmRowImportStatus.Ok;
}

/// <summary>Outcome of reading one workbook.</summary>
public record BmParseResult(
    string FileName,
    string? SheetName,
    BmHeaderMap HeaderMap,
    List<ParsedBmRow> Rows,
    List<string> FileErrors)
{
    /// <summary>True when the workbook could not be read at all — nothing to preview.</summary>
    public bool HasFatalError => FileErrors.Count > 0;

    public int OkCount => Rows.Count(r => r.IsOk);

    public int ErrorCount => FileErrors.Count + Rows.Count(r => !r.IsOk);

    public string Summary => $"{Rows.Count} rows · {OkCount} OK · {ErrorCount} errors";
}

/// <summary>Column headers discovered from inspecting a workbook.</summary>
public record BmHeaderInspection(
    string? SheetName,
    List<string> AllHeaders,
    string? DetectedItemCodeColumn,
    string? SuggestedDescriptionColumn,
    string? DetectedQuantityColumn,
    string? DetectedDueDateColumn,
    string? DetectedSalesOrderColumn = null,
    string? DetectedPONoColumn = null);

/// <summary>
/// Reads a BM .xlsx into preview rows: header mapping, row reading, quantity / date
/// parsing and product-code lookup (PRD BM-2, Architecture §7.3). Writes nothing —
/// the preview screen persists the rows and confirm-import creates the Jobs (P4.5/P4.6).
/// </summary>
public interface IBmImportService
{
    /// <summary>The configured header map, or the default when the setting is absent.</summary>
    Task<BmHeaderMap> GetHeaderMapAsync(CancellationToken ct = default);

    Task<BmHeaderInspection> InspectHeadersAsync(Stream stream, CancellationToken ct = default);

    Task<BmParseResult> ParseAsync(Stream stream, string fileName, CancellationToken ct = default);

    Task<BmParseResult> ParseAsync(Stream stream, string fileName, BmHeaderMap? customMap, CancellationToken ct = default);
}

public class BmImportService : IBmImportService
{
    private readonly AppDbContext _db;

    public BmImportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BmHeaderMap> GetHeaderMapAsync(CancellationToken ct = default)
    {
        var json = await _db.Settings
            .Where(s => s.Key == BmSettings.HeaderMapKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json)) return BmHeaderMap.Default;

        try
        {
            return JsonSerializer.Deserialize<BmHeaderMap>(json) ?? BmHeaderMap.Default;
        }
        catch (JsonException)
        {
            // A corrupt setting must not take the importer down.
            return BmHeaderMap.Default;
        }
    }

    public async Task<BmHeaderInspection> InspectHeadersAsync(Stream stream, CancellationToken ct = default)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch
        {
            return new BmHeaderInspection(null, [], null, null, null, null);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet == null)
                return new BmHeaderInspection(null, [], null, null, null, null);

            var headerRow = sheet.FirstRowUsed();
            if (headerRow == null)
                return new BmHeaderInspection(sheet.Name, [], null, null, null, null);

            var headerRowNo = headerRow.RowNumber();
            var lastColumnNo = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var headers = new List<string>();

            for (var col = 1; col <= lastColumnNo; col++)
            {
                var text = ReadText(sheet.Cell(headerRowNo, col));
                if (!string.IsNullOrWhiteSpace(text))
                    headers.Add(text);
            }

            var itemCode = FindBestMatch(headers, ItemCodeAliases);
            var desc = FindBestMatch(headers, DescriptionAliases);
            var qty = FindBestMatch(headers, QuantityAliases);
            var due = FindBestMatch(headers, DueDateAliases);
            var salesOrder = FindBestMatch(headers, SalesOrderAliases);
            var poNo = FindBestMatch(headers, PONoAliases);

            return new BmHeaderInspection(
                sheet.Name,
                headers,
                itemCode,
                desc,
                qty,
                due,
                salesOrder,
                poNo);
        }
    }

    private static string? FindBestMatch(IEnumerable<string> headers, string[] candidates)
    {
        foreach (var c in candidates)
        {
            var match = headers.FirstOrDefault(h => string.Equals(h.Trim(), c, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        foreach (var c in candidates)
        {
            var match = headers.FirstOrDefault(h => h.Trim().Contains(c, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return null;
    }

    public Task<BmParseResult> ParseAsync(Stream stream, string fileName, CancellationToken ct = default)
        => ParseAsync(stream, fileName, null, ct);

    public async Task<BmParseResult> ParseAsync(Stream stream, string fileName, BmHeaderMap? customMap, CancellationToken ct = default)
    {
        var map = customMap ?? await GetHeaderMapAsync(ct);

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            return new BmParseResult(
                fileName, null, map, [],
                ["The file is not a valid .xlsx workbook."]);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet == null)
            {
                return new BmParseResult(
                    fileName, null, map, [],
                    ["The workbook contains no worksheets."]);
            }

            return await ParseSheetAsync(sheet, fileName, map, await GetActiveColourMapAsync(ct), ct);
        }
    }

    // -----------------------------------------------------------------------
    // Reading
    // -----------------------------------------------------------------------

    private async Task<List<PriorityColourMap>> GetActiveColourMapAsync(CancellationToken ct)
        => await _db.PriorityColourMap
            .Where(m => m.IsActive)
            .OrderBy(m => m.Priority)
            .ToListAsync(ct);

    private async Task<BmParseResult> ParseSheetAsync(
        IXLWorksheet sheet,
        string fileName,
        BmHeaderMap map,
        List<PriorityColourMap> colourMap,
        CancellationToken ct)
    {
        var fileErrors = new List<string>();

        var headerRow = sheet.FirstRowUsed();
        if (headerRow == null)
        {
            return new BmParseResult(
                fileName, sheet.Name, map, [],
                ["The first worksheet is empty."]);
        }

        var headerRowNo = headerRow.RowNumber();
        var lastColumnNo = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        var columns = MapColumns(sheet, headerRowNo, lastColumnNo, map);

        // Without a product code column nothing can be matched, so stop here.
        if (!columns.ContainsKey(BmField.ProductCode))
        {
            fileErrors.Add(
                $"Could not find a product code column in row {headerRowNo}. " +
                $"Expected the heading \"{map.ProductCode}\" or \"Item Code\".");
            return new BmParseResult(fileName, sheet.Name, map, [], fileErrors);
        }

        var lastRowNo = sheet.LastRowUsed()?.RowNumber() ?? headerRowNo;
        var raw = new List<RawRow>();

        for (var rowNo = headerRowNo + 1; rowNo <= lastRowNo; rowNo++)
        {
            var codeCell = CellFor(sheet, columns, BmField.ProductCode, rowNo);
            var qtyCell = CellFor(sheet, columns, BmField.Quantity, rowNo);
            var dateCell = CellFor(sheet, columns, BmField.DueDate, rowNo);
            var remarksCell = CellFor(sheet, columns, BmField.Remarks, rowNo);
            var soCell = CellFor(sheet, columns, BmField.SalesOrder, rowNo);
            var poCell = CellFor(sheet, columns, BmField.PONo, rowNo);

            var code = ReadText(codeCell);
            var qtyText = ReadText(qtyCell);
            var dateText = ReadText(dateCell);
            var remarks = ReadText(remarksCell);
            var salesOrder = ReadText(soCell);
            var poNo = ReadText(poCell);

            // Architecture §7.3: ignore rows that carry nothing at all.
            if (code.Length == 0 && qtyText.Length == 0 && dateText.Length == 0 && remarks.Length == 0 && salesOrder.Length == 0 && poNo.Length == 0)
                continue;

            var (quantity, quantityError) = ParseQuantity(qtyCell, qtyText);
            var (dueDate, dateError) = ParseDate(dateCell, dateText);

            raw.Add(new RawRow(
                rowNo,
                code,
                quantity,
                quantityError,
                dueDate,
                dateError,
                remarks.Length == 0 ? null : remarks,
                BmColourDetector.Detect(sheet.Workbook, codeCell, sheet.Row(rowNo)),
                salesOrder.Length == 0 ? null : salesOrder,
                poNo.Length == 0 ? null : poNo));
        }

        var rows = await ValidateAsync(raw, colourMap, ct);
        return new BmParseResult(fileName, sheet.Name, map, rows, fileErrors);
    }

    private static readonly string[] ItemCodeAliases =
    [
        "Item Code", "ItemCode"
    ];

    private static readonly string[] QuantityAliases =
    [
        "Qty.Reqd", "Qty.Pend", "Quantity", "Qty", "Order Qty", "Reqd Qty", "Required Qty"
    ];

    private static readonly string[] DueDateAliases =
    [
        "Due Date", "DueDate", "Delivery Date", "Due", "Target Date"
    ];

    private static readonly string[] DescriptionAliases =
    [
        "Finished Good Item Desc", "Item Description", "Description", "Remarks", "Item Desc", "Desc", "Material Description", "FG Description"
    ];

    private static readonly string[] SalesOrderAliases =
    [
        "Sales Order", "SalesOrder", "Sales Order No", "SO No", "SO Number", "SO"
    ];

    private static readonly string[] PONoAliases =
    [
        "PONo", "PO No", "PO Number", "Purchase Order", "Purchase Order No", "PO"
    ];

    private static Dictionary<string, int> MapColumns(
        IXLWorksheet sheet,
        int headerRowNo,
        int lastColumnNo,
        BmHeaderMap map)
    {
        var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BmField.ProductCode] = map.ProductCode,
            [BmField.Quantity] = map.Quantity,
            [BmField.DueDate] = map.DueDate,
            [BmField.Remarks] = map.Remarks,
            [BmField.SalesOrder] = map.SalesOrder,
            [BmField.PONo] = map.PONo
        };

        var found = new Dictionary<string, int>();

        var columnHeadings = new Dictionary<int, string>();
        for (var columnNo = 1; columnNo <= lastColumnNo; columnNo++)
        {
            var heading = ReadText(sheet.Cell(headerRowNo, columnNo));
            if (heading.Length > 0)
                columnHeadings[columnNo] = heading;
        }

        // 1. Try exact matches from map
        foreach (var (columnNo, heading) in columnHeadings)
        {
            foreach (var (field, label) in wanted)
            {
                if (!found.ContainsKey(field) &&
                    string.Equals(heading, label.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    found[field] = columnNo;
                }
            }
        }

        // 2. If ProductCode not found, search common aliases (Item Code, etc.)
        if (!found.ContainsKey(BmField.ProductCode))
            MatchAlias(columnHeadings, found, BmField.ProductCode, ItemCodeAliases);

        // 3. If Quantity not found, search aliases (Qty.Reqd, Qty.Pend, etc.)
        if (!found.ContainsKey(BmField.Quantity))
            MatchAlias(columnHeadings, found, BmField.Quantity, QuantityAliases);

        // 4. If DueDate not found, search aliases (Due Date, etc.)
        if (!found.ContainsKey(BmField.DueDate))
            MatchAlias(columnHeadings, found, BmField.DueDate, DueDateAliases);

        // 5. If Remarks not found, search aliases (Finished Good Item Desc, Description, etc.)
        if (!found.ContainsKey(BmField.Remarks))
            MatchAlias(columnHeadings, found, BmField.Remarks, DescriptionAliases);

        // 6. If SalesOrder not found, search aliases (Sales Order, SO No, etc.)
        if (!found.ContainsKey(BmField.SalesOrder))
            MatchAlias(columnHeadings, found, BmField.SalesOrder, SalesOrderAliases);

        // 7. If PONo not found, search aliases (PONo, PO No, etc.)
        if (!found.ContainsKey(BmField.PONo))
            MatchAlias(columnHeadings, found, BmField.PONo, PONoAliases);

        return found;
    }

    private static void MatchAlias(
        Dictionary<int, string> columnHeadings,
        Dictionary<string, int> found,
        string field,
        string[] aliases)
    {
        foreach (var alias in aliases)
        {
            foreach (var (columnNo, heading) in columnHeadings)
            {
                if (found.Values.Contains(columnNo)) continue;

                if (string.Equals(heading, alias, StringComparison.OrdinalIgnoreCase))
                {
                    found[field] = columnNo;
                    return;
                }
            }
        }

        // Partial match fallback if still not found
        foreach (var alias in aliases)
        {
            foreach (var (columnNo, heading) in columnHeadings)
            {
                if (found.Values.Contains(columnNo)) continue;

                if (heading.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    found[field] = columnNo;
                    return;
                }
            }
        }
    }

    private static IXLCell? CellFor(
        IXLWorksheet sheet,
        Dictionary<string, int> columns,
        string field,
        int rowNo)
        => columns.TryGetValue(field, out var columnNo) ? sheet.Cell(rowNo, columnNo) : null;

    /// <summary>Cell text without ever throwing — ClosedXML refuses to stringify some types.</summary>
    private static string ReadText(IXLCell? cell)
    {
        if (cell == null) return string.Empty;

        try
        {
            return cell.DataType == XLDataType.Blank
                ? string.Empty
                : cell.Value.ToString().Trim();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static (int? Quantity, bool IsInvalid) ParseQuantity(IXLCell? cell, string text)
    {
        // Empty is fine: the row is either flagged by another field or left blank.
        if (text.Length == 0) return (null, false);

        double? numeric = null;

        if (cell != null && cell.DataType == XLDataType.Number)
        {
            numeric = cell.GetDouble();
        }
        else if (double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            numeric = parsed;
        }

        if (numeric is null) return (null, true);
        if (Math.Abs(numeric.Value - Math.Round(numeric.Value)) > 0.0001) return (null, true);

        var quantity = (int)Math.Round(numeric.Value);
        return quantity <= 0 ? (null, true) : (quantity, false);
    }

    private static (DateOnly? DueDate, bool IsInvalid) ParseDate(IXLCell? cell, string text)
    {
        // PRD BM-2: a real date, an Excel serial number, or text.
        if (cell != null && cell.DataType == XLDataType.DateTime)
            return (DateOnly.FromDateTime(cell.GetDateTime()), false);

        if (cell != null && cell.DataType == XLDataType.Number)
        {
            try
            {
                return (DateOnly.FromDateTime(DateTime.FromOADate(cell.GetDouble())), false);
            }
            catch (ArgumentOutOfRangeException)
            {
                return (null, true);
            }
        }

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var current))
            return (DateOnly.FromDateTime(current), false);

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant))
            return (DateOnly.FromDateTime(invariant), false);

        return (null, true);
    }

    // -----------------------------------------------------------------------
    // Colour detection and priority matching
    // -----------------------------------------------------------------------

    private async Task<List<ParsedBmRow>> ValidateAsync(
        List<RawRow> raw,
        List<PriorityColourMap> colourMap,
        CancellationToken ct)
    {
        var codes = raw
            .Select(r => r.ProductCode)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var known = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (codes.Count > 0)
        {
            var products = await _db.Products
                .Where(p => codes.Contains(p.ProductCode))
                .ToListAsync(ct);

            foreach (var product in products)
                known[product.ProductCode] = product.IsActive;
        }

        return raw.Select(row =>
        {
            var messages = new List<string>();

            if (row.ProductCode.Length == 0)
            {
                messages.Add("Product code is required.");
            }
            else if (!known.TryGetValue(row.ProductCode, out var isActive))
            {
                messages.Add($"Product '{row.ProductCode}' was not found.");
            }
            else if (!isActive)
            {
                messages.Add($"Product '{row.ProductCode}' is not active.");
            }

            if (row.QuantityInvalid)
                messages.Add("Quantity must be a whole number greater than 0.");

            if (row.DateInvalid)
                messages.Add("Due date is not a valid date.");

            var match = ColourMatcher.Resolve(row.ColourHex, colourMap);

            if (messages.Count > 0)
            {
                return new ParsedBmRow(
                    row.ExcelRowNo,
                    row.ProductCode,
                    row.Quantity,
                    row.DueDate,
                    row.Remarks,
                    row.ColourHex,
                    match.Priority,
                    BmRowImportStatus.Error,
                    string.Join(' ', messages),
                    row.SalesOrder,
                    row.PONo);
            }

            // An unrecognised colour still imports, but the Admin should see that the
            // row was defaulted to White rather than silently losing its priority.
            if (!match.WithinTolerance)
            {
                return new ParsedBmRow(
                    row.ExcelRowNo,
                    row.ProductCode,
                    row.Quantity,
                    row.DueDate,
                    row.Remarks,
                    row.ColourHex,
                    match.Priority,
                    BmRowImportStatus.Warning,
                    $"Colour {row.ColourHex} does not match any shade; the row is treated as White.",
                    row.SalesOrder,
                    row.PONo);
            }

            return new ParsedBmRow(
                row.ExcelRowNo,
                row.ProductCode,
                row.Quantity,
                row.DueDate,
                row.Remarks,
                row.ColourHex,
                match.Priority,
                BmRowImportStatus.Ok,
                null,
                row.SalesOrder,
                row.PONo);
        }).ToList();
    }

    // -----------------------------------------------------------------------

    private static class BmField
    {
        public const string ProductCode = nameof(ProductCode);
        public const string Quantity = nameof(Quantity);
        public const string DueDate = nameof(DueDate);
        public const string Remarks = nameof(Remarks);
        public const string SalesOrder = nameof(SalesOrder);
        public const string PONo = nameof(PONo);
    }

    private record RawRow(
        int ExcelRowNo,
        string ProductCode,
        int? Quantity,
        bool QuantityInvalid,
        DateOnly? DueDate,
        bool DateInvalid,
        string? Remarks,
        string? ColourHex,
        string? SalesOrder,
        string? PONo);
}