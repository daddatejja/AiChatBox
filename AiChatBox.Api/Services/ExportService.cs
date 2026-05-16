using MiniExcelLibs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;
using System.Data;

namespace AiChatBox.Api.Services
{
    public class ExportService
    {
        private string SafeWrap(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "-";
            if (input.Length < 5) return input;
            
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                sb.Append(input[i]);
                if ((i + 1) % 2 == 0) sb.Append('\u200B');
            }
            return sb.ToString();
        }

        public byte[] ExportToExcel(IEnumerable<IDictionary<string, object>> data)
        {
            using var ms = new MemoryStream();
            ms.SaveAs(data);
            return ms.ToArray();
        }

        public byte[] ExportToPdf(string title, IEnumerable<IDictionary<string, object>> data)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana).FontColor(Colors.Grey.Darken3));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(title).SemiBold().FontSize(24).FontColor(Colors.Indigo.Medium);
                            col.Item().Text($"{DateTime.Now:MMMM dd, yyyy}").FontSize(10).FontColor(Colors.Grey.Medium);
                        });

                        row.ConstantItem(100).AlignRight().Column(col =>
                        {
                            col.Item().Text("AiChatBox").SemiBold().FontSize(14).FontColor(Colors.Indigo.Medium);
                            col.Item().Text("AI Reporting").FontSize(8).FontColor(Colors.Grey.Lighten1);
                        });
                    });

                    page.Content().PaddingVertical(20).Table(table =>
                    {
                        var firstRow = data.FirstOrDefault();
                        if (firstRow == null) return;

                        var columns = firstRow.Keys.ToList();
                        var columnCount = columns.Count;
                        var fontSize = columnCount > 10 ? 7 : (columnCount > 5 ? 8 : 10);

                        table.ColumnsDefinition(columnsDefinition =>
                        {
                            foreach (var col in columns)
                            {
                                columnsDefinition.RelativeColumn();
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var col in columns)
                            {
                                header.Cell().Element(HeaderStyle).Text(SafeWrap(col)).FontSize(fontSize).SemiBold().FontColor(Colors.White);
                            }

                            static IContainer HeaderStyle(IContainer container)
                            {
                                return container
                                    .Background(Colors.Indigo.Medium)
                                    .PaddingVertical(8)
                                    .PaddingHorizontal(5)
                                    .AlignMiddle()
                                    .AlignCenter();
                            }
                        });

                        var rowIndex = 0;
                        foreach (var row in data)
                        {
                            var backgroundColor = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                            foreach (var col in columns)
                            {
                                var text = row[col]?.ToString() ?? "-";
                                table.Cell().Background(backgroundColor).Element(CellStyle).Text(SafeWrap(text)).FontSize(fontSize);
                            }

                            static IContainer CellStyle(IContainer container)
                            {
                                return container
                                    .BorderBottom(0.5f)
                                    .BorderColor(Colors.Grey.Lighten3)
                                    .PaddingVertical(6)
                                    .PaddingHorizontal(2)
                                    .AlignMiddle();
                            }
                            rowIndex++;
                        }
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text(x =>
                        {
                            x.Span("Generated by ").FontSize(8).FontColor(Colors.Grey.Medium);
                            x.Span("AiChatBox").SemiBold().FontSize(8).FontColor(Colors.Indigo.Lighten2);
                        });

                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
