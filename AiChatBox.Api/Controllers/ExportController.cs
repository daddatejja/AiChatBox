using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AiChatBox.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExportController(ExportService exportService) : ControllerBase
    {
        private readonly ExportService _exportService = exportService;

        [HttpPost("excel")]
        public IActionResult ExportExcel([FromBody] ExportRequest request)
        {
            try
            {
                var json = request.Data.ToString();
                var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                var bytes = _exportService.ExportToExcel(data);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{request.FileName ?? "export"}.xlsx");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("pdf")]
        public IActionResult ExportPdf([FromBody] ExportRequest request)
        {
            try
            {
                var json = request.Data.ToString();
                var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                var bytes = _exportService.ExportToPdf(request.Title ?? "Data Report", data);
                return File(bytes, "application/pdf", $"{request.FileName ?? "export"}.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    public class ExportRequest
    {
        public object Data { get; set; }
        public string? Title { get; set; }
        public string? FileName { get; set; }
    }
}
