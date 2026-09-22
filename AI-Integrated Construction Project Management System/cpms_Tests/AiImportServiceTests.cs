using cpms_Application.Interfaces;
using cpms_Application.Response.AiConstructionPlanner;
using cpms_Application.Services;
using cpms_Domain;
using cpms_Domain.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace cpms_Tests;

public class AiImportServiceTests
{
    [Fact]
    public async Task ImportReturnsDraftWithoutPersisting()
    {
        var uow = new TestUnitOfWork();
        var service = CreateService(uow);

        var response = await service.ImportProjectFromWordAiAsync(CreateDocxFile("Tên dự án: Riverside House"));

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var draft = Assert.IsType<AiImportPreviewResponse>(response.Result);
        Assert.Equal("Riverside House", draft.Project.ProjectName);
        Assert.Equal("District 2", draft.Project.Address);
        Assert.Equal(2000000000, draft.Project.TotalBudget);
        var phase = Assert.Single(draft.Plan.Phases);
        Assert.Equal("PH-P01", phase.TempId);
        Assert.Equal("P01", phase.AiKey);
        Assert.Equal("Foundation", phase.Name);
        Assert.Equal(2, draft.Plan.Tasks.Count);
        Assert.All(draft.Plan.Tasks, t => Assert.Equal("PH-P01", t.PhaseTempId));
        Assert.Equal("Excavate", draft.Plan.Tasks[0].TaskName);
        Assert.Empty(uow.ProjectRecords);
        Assert.Empty(uow.PhaseRecords);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task ImportRejectsInvalidJson()
    {
        var uow = new TestUnitOfWork();
        var service = CreateService(uow, "Here is your project, but not JSON.");

        var response = await service.ImportProjectFromWordAiAsync(CreateDocxFile("Some text"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid import JSON", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportRejectsMissingProjectName()
    {
        var uow = new TestUnitOfWork();
        var service = CreateService(uow, """{ "address": "Nowhere", "phases": [], "tasks": [] }""");

        var response = await service.ImportProjectFromWordAiAsync(CreateDocxFile("Some text"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("project name", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportRejectsUnknownPhaseRef()
    {
        var uow = new TestUnitOfWork();
        var service = CreateService(uow, """
{
  "projectName": "P",
  "baselineStart": "2026-11-01",
  "baselineEnd": "2027-04-30",
  "phases": [{ "name": "Foundation", "sequenceOrder": 0, "baselineStart": "2026-11-01", "baselineEnd": "2026-12-15" }],
  "tasks": [{ "phaseRef": "Nope", "taskName": "Ghost task", "plannedBudget": 1, "baselineStart": "2026-11-01", "baselineEnd": "2026-11-02" }]
}
""");

        var response = await service.ImportProjectFromWordAiAsync(CreateDocxFile("Some text"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Ghost task", response.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportRejectsNonDocxEmptyAndOversizedFiles()
    {
        var uow = new TestUnitOfWork();
        var service = CreateService(uow);

        var notDocx = await service.ImportProjectFromWordAiAsync(
            new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "file", "plan.pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, notDocx.StatusCode);

        var empty = await service.ImportProjectFromWordAiAsync(
            new FormFile(new MemoryStream(), 0, 0, "file", "plan.docx"));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    private static AiConstructionPlannerService CreateService(
        TestUnitOfWork uow,
        string? extractionJson = null,
        IClaimService? claimService = null) =>
        new(
            uow,
            claimService ?? new FakeClaimService(7, Role.PM),
            new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success(extractionJson ?? ValidExtractionJson) });

    private static IFormFile CreateDocxFile(params string[] paragraphs)
    {
        byte[] bytes;
        using (var output = new MemoryStream())
        {
            using (var document = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Document(new Body(
                    paragraphs.Select(t => new Paragraph(new Run(new Text(t)))).ToArray()));
                main.Document.Save();
            }
            bytes = output.ToArray();
        }
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "file", "plan.docx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private const string ValidExtractionJson = """
{
  "projectName": "Riverside House",
  "address": "District 2",
  "totalBudget": 2000000000,
  "currency": "VND",
  "startDate": "2026-11-01",
  "baselineStart": "2026-11-01",
  "baselineEnd": "2027-04-30",
  "phases": [
    { "name": "Foundation", "description": "Ground works", "sequenceOrder": 0, "baselineStart": "2026-11-01", "baselineEnd": "2026-12-15" }
  ],
  "tasks": [
    { "phaseRef": "Foundation", "taskName": "Excavate", "plannedBudget": 50000000, "baselineStart": "2026-11-01", "baselineEnd": "2026-11-20" },
    { "phaseRef": "1", "taskName": "Pour concrete", "plannedBudget": 80000000, "baselineStart": "2026-11-21", "baselineEnd": "2026-12-10" }
  ]
}
""";
}
