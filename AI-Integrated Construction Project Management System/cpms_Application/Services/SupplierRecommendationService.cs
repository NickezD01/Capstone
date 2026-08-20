using cpms_Application.Interfaces;
using cpms_Application.Request.SupplierRecommendation;
using cpms_Application.Response;
using cpms_Application.Response.SupplierRecommendation;
using cpms_Domain;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace cpms_Application.Services
{
    public class SupplierRecommendationService : ISupplierRecommendationService
    {
        private readonly IUnitOfWork _uow;
        private readonly IGoogleAIClient _googleAIClient;
        private readonly ITavilySearchClient _tavilySearchClient;
        private readonly AppSetting _appSetting;

        public SupplierRecommendationService(
            IUnitOfWork uow,
            IGoogleAIClient googleAIClient,
            ITavilySearchClient tavilySearchClient,
            AppSetting appSetting)
        {
            _uow = uow;
            _googleAIClient = googleAIClient;
            _tavilySearchClient = tavilySearchClient;
            _appSetting = appSetting;
        }

        public async Task<ApiResponse> RecommendBalancedSuppliersAsync(BalancedSupplierRecommendationRequest request)
        {
            var response = new ApiResponse();
            if (request.Items == null || request.Items.Count == 0)
                return response.SetBadRequest("At least one requested material is required.");

            var requestedItems = request.Items
                .Where(i => i.MaterialId > 0)
                .GroupBy(i => i.MaterialId)
                .Select(g => new RequestedMaterialItem
                {
                    MaterialId = g.Key,
                    Quantity = g.Sum(x => x.Quantity <= 0 ? 1 : x.Quantity)
                })
                .ToList();

            if (requestedItems.Count == 0)
                return response.SetBadRequest("Requested material IDs are invalid.");

            var materialIds = requestedItems.Select(i => i.MaterialId).ToList();
            var catalogs = await _uow.SupplierCatalogs.GetAllAsync(
                filter: c => c.IsAvailable && materialIds.Contains(c.Variant.MaterialId),
                include: query => query
                    .Include(c => c.Variant)
                    .ThenInclude(v => v.Material)
                    .Include(c => c.Supplier)
                    .ThenInclude(s => s.SupplierMetric)
            );

            if (catalogs.Count == 0)
                return response.SetNotFound("No supplier catalog entries match the requested materials.");

            var candidates = BuildCandidates(catalogs, requestedItems, request);
            var fallbackRecommendations = candidates
                .OrderByDescending(c => c.BalancedScore)
                .Take(Math.Clamp(request.MaxRecommendations, 1, 20))
                .Select(c => c.Response)
                .ToList();

            var result = new BalancedSupplierRecommendationResponse
            {
                Recommendations = fallbackRecommendations
            };

            var webSearchResult = await TryGetNearbyWebSuppliersAsync(request, requestedItems, result.Recommendations);
            if (webSearchResult != null)
            {
                result.UsedWebSearch = true;
                result.WebSearchSummary = webSearchResult.Summary;
                result.Recommendations = result.Recommendations
                    .Concat(webSearchResult.Recommendations)
                    .OrderByDescending(r => r.BalancedScore)
                    .Take(Math.Clamp(request.MaxRecommendations, 1, 20))
                    .ToList();
            }

            var aiResult = await TryApplyAiRankingAsync(result.Recommendations);
            if (aiResult != null)
            {
                result.UsedGoogleAI = true;
                result.AiSummary = aiResult.Summary;
                result.Recommendations = aiResult.Recommendations;
            }

            return response.SetOk(result);
        }

        private async Task<AiWebSupplierResult?> TryGetNearbyWebSuppliersAsync(
            BalancedSupplierRecommendationRequest request,
            List<RequestedMaterialItem> requestedItems,
            List<SupplierRecommendationResponse> internalRecommendations)
        {
            if (!request.SearchWebForNearbySuppliers || string.IsNullOrWhiteSpace(request.WarehouseLocation))
                return null;

            var materialIds = requestedItems.Select(i => i.MaterialId).ToList();
            var materials = await _uow.Materials.GetAllAsync(m => materialIds.Contains(m.MaterialId));
            var materialNames = materials
                .Select(m => new
                {
                    m.MaterialId,
                    m.MaterialName,
                    Quantity = requestedItems.First(i => i.MaterialId == m.MaterialId).Quantity
                })
                .ToList();

            var systemInstruction = "You analyze web search results for construction material suppliers near a warehouse location. Return only valid JSON, no markdown. Prefer suppliers with evidence of material fit, reliability, reviews, and contact details.";
            var searchQuery = BuildSupplierSearchQuery(request, materialNames.Select(m => m.MaterialName).ToList());
            var searchResult = await _tavilySearchClient.SearchAsync(new TavilySearchOptions
            {
                Query = searchQuery,
                MaxResults = Math.Clamp(_appSetting.Tavily.DefaultMaxResults + 3, 5, 20),
                SearchDepth = _appSetting.Tavily.SearchDepth
            });

            if (!searchResult.IsSuccess)
                return null;

            var input = JsonSerializer.Serialize(new
            {
                task = "Find nearby external suppliers that can provide the requested construction materials. Score each supplier for balanced cost and reliable material supply. Use only the provided web search results and include source URLs from those results.",
                warehouseLocation = request.WarehouseLocation,
                radiusKm = request.SearchRadiusKm <= 0 ? 30 : request.SearchRadiusKm,
                regionCode = request.RegionCode,
                requestedMaterials = materialNames,
                existingInternalSupplierNames = internalRecommendations.Select(r => r.CompanyName).ToList(),
                webSearchResults = searchResult.Results.Select(r => new
                {
                    r.Title,
                    r.Url,
                    r.Content,
                    r.Score
                }),
                requiredJsonShape = new
                {
                    summary = "short summary of web search results",
                    suppliers = new[]
                    {
                        new
                        {
                            companyName = "supplier name",
                            address = "address if found",
                            contactPhone = "phone if found",
                            contactEmail = "email if found",
                            websiteUrl = "website if found",
                            googleMapsUrl = "maps/profile url if found",
                            rating = 4.5,
                            reviewCount = 120,
                            distanceEstimate = "about 12 km from warehouse",
                            estimatedCostLevel = "low|medium|high|unknown",
                            reliabilityLevel = "low|medium|high|unknown",
                            matchedMaterials = new[] { "cement", "steel" },
                            balancedScore = 82.5,
                            reason = "short reason",
                            sourceUrls = new[] { "https://example.com" }
                        }
                    }
                }
            });

            var aiResponse = await _googleAIClient.GenerateTextAsync(systemInstruction, input);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Text))
                return null;

            var json = ExtractJson(aiResponse.Text);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var parsed = JsonSerializer.Deserialize<WebSupplierEnvelope>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed?.Suppliers == null || parsed.Suppliers.Count == 0)
                    return null;

                var requestedCount = requestedItems.Count;
                var recommendations = parsed.Suppliers
                    .Where(s => !string.IsNullOrWhiteSpace(s.CompanyName))
                    .Select(s => new SupplierRecommendationResponse
                    {
                        SupplierId = 0,
                        Source = "WebSearch",
                        CompanyName = s.CompanyName!,
                        ContactEmail = s.ContactEmail,
                        ContactPhone = s.ContactPhone,
                        Address = s.Address,
                        WebsiteUrl = s.WebsiteUrl,
                        GoogleMapsUrl = s.GoogleMapsUrl,
                        Rating = s.Rating,
                        ReviewCount = s.ReviewCount,
                        DistanceEstimate = s.DistanceEstimate,
                        BalancedScore = Clamp(s.BalancedScore ?? EstimateWebScore(s), 0, 100),
                        ReliabilityScore = EstimateReliabilityScore(s),
                        MatchedMaterialCount = s.MatchedMaterials?.Count ?? 0,
                        RequestedMaterialCount = requestedCount,
                        Reason = string.IsNullOrWhiteSpace(s.Reason) ? "External supplier found through Tavily web search." : s.Reason!,
                        SourceUrls = s.SourceUrls ?? new List<string>()
                    })
                    .ToList();

                return new AiWebSupplierResult
                {
                    Summary = parsed.Summary,
                    Recommendations = recommendations
                };
            }
            catch
            {
                return null;
            }
        }

        private static string BuildSupplierSearchQuery(
            BalancedSupplierRecommendationRequest request,
            IReadOnlyList<string> materialNames)
        {
            var materials = materialNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Take(5)
                .ToList();

            var materialText = materials.Count == 0 ? "construction materials" : string.Join(", ", materials);
            var radiusKm = request.SearchRadiusKm <= 0 ? 30 : request.SearchRadiusKm;
            var location = request.WarehouseLocation!.Trim();
            var region = string.IsNullOrWhiteSpace(request.RegionCode) ? string.Empty : $" {request.RegionCode.Trim()}";

            return $"construction material suppliers near {location}{region} within {radiusKm} km supplying {materialText}";
        }

        private static List<SupplierCandidate> BuildCandidates(
            List<SupplierCatalog> catalogs,
            List<RequestedMaterialItem> requestedItems,
            BalancedSupplierRecommendationRequest request)
        {
            var requestedCount = requestedItems.Count;
            var requestedByMaterialId = requestedItems.ToDictionary(i => i.MaterialId, i => i);

            var initialCandidates = catalogs
                .GroupBy(c => c.SupplierId)
                .Select(group =>
                {
                    var supplier = group.First().Supplier;
                    var lines = group
                        .Where(c => requestedByMaterialId.ContainsKey(c.Variant.MaterialId))
                        .Select(c =>
                        {
                            var materialId = c.Variant.MaterialId;
                            var item = requestedByMaterialId[materialId];
                            var materialName = string.IsNullOrWhiteSpace(c.Variant.VariantName)
                                ? c.Variant.Material.MaterialName
                                : $"{c.Variant.Material.MaterialName} - {c.Variant.VariantName}";
                            return new SupplierRecommendationLineResponse
                            {
                                MaterialId = materialId,
                                MaterialName = materialName,
                                Quantity = item.Quantity,
                                UnitPrice = c.UnitPrice,
                                EstimatedLineCost = c.UnitPrice * item.Quantity,
                                LeadTimeDays = c.LeadTimeDays
                            };
                        })
                        .ToList();

                    var metric = supplier.SupplierMetric;
                    var estimatedCost = lines.Sum(l => l.EstimatedLineCost);
                    var avgLeadTime = lines.Count == 0 ? 999 : lines.Average(l => l.LeadTimeDays);
                    var reliability = metric?.ReliabilityScore ?? 50;
                    var defectRate = metric?.DefectRatePct ?? 0;
                    var deliveryDelay = metric?.AvgDeliveryDelay ?? 0;
                    var reliabilityAdjusted = Clamp(reliability - (defectRate * 0.5) - (Math.Max(0, deliveryDelay) * 2), 0, 100);

                    return new SupplierCandidate
                    {
                        SupplierId = supplier.SupplierId,
                        EstimatedTotalCost = estimatedCost,
                        AverageLeadTimeDays = avgLeadTime,
                        ReliabilityScore = reliabilityAdjusted,
                        DefectRatePct = defectRate,
                        AvgDeliveryDelay = deliveryDelay,
                        MatchedMaterialCount = lines.Select(l => l.MaterialId).Distinct().Count(),
                        RequestedMaterialCount = requestedCount,
                        Response = new SupplierRecommendationResponse
                        {
                            SupplierId = supplier.SupplierId,
                            CompanyName = supplier.CompanyName,
                            ContactEmail = supplier.ContactEmail,
                            ContactPhone = supplier.ContactPhone,
                            Address = supplier.Address,
                            EstimatedTotalCost = estimatedCost,
                            AverageLeadTimeDays = avgLeadTime,
                            ReliabilityScore = reliabilityAdjusted,
                            DefectRatePct = defectRate,
                            AvgDeliveryDelay = deliveryDelay,
                            MatchedMaterialCount = lines.Select(l => l.MaterialId).Distinct().Count(),
                            RequestedMaterialCount = requestedCount,
                            Lines = lines
                        }
                    };
                })
                .ToList();

            var minCost = initialCandidates.Min(c => c.EstimatedTotalCost);
            var maxCost = initialCandidates.Max(c => c.EstimatedTotalCost);
            var minLead = initialCandidates.Min(c => c.AverageLeadTimeDays);
            var maxLead = initialCandidates.Max(c => c.AverageLeadTimeDays);
            var weightSum = request.CostWeight + request.ReliabilityWeight + request.LeadTimeWeight;
            if (weightSum <= 0) weightSum = 1;

            foreach (var candidate in initialCandidates)
            {
                var costScore = NormalizeInverse((double)candidate.EstimatedTotalCost, (double)minCost, (double)maxCost);
                var leadTimeScore = NormalizeInverse(candidate.AverageLeadTimeDays, minLead, maxLead);
                var coverageScore = candidate.RequestedMaterialCount == 0
                    ? 0
                    : (candidate.MatchedMaterialCount / (double)candidate.RequestedMaterialCount) * 100;

                var weightedScore =
                    (costScore * request.CostWeight / weightSum) +
                    (candidate.ReliabilityScore * request.ReliabilityWeight / weightSum) +
                    (leadTimeScore * request.LeadTimeWeight / weightSum);

                candidate.BalancedScore = Math.Round((weightedScore * 0.8) + (coverageScore * 0.2), 2);
                candidate.Response.BalancedScore = candidate.BalancedScore;
                candidate.Response.Reason = BuildReason(candidate.Response);
            }

            return initialCandidates;
        }

        private async Task<AiRankingResult?> TryApplyAiRankingAsync(List<SupplierRecommendationResponse> fallbackRecommendations)
        {
            var systemInstruction = "You rank construction material suppliers. Return only valid JSON, no markdown.";
            var input = JsonSerializer.Serialize(new
            {
                task = "Rerank suppliers for a balanced choice of cost and reliable material supply. Keep only supplier IDs from the provided candidates.",
                requiredJsonShape = new
                {
                    summary = "short overall summary",
                    recommendations = new[]
                    {
                        new { supplierId = 1, companyName = "supplier name", source = "InternalCatalog or WebSearch", reason = "short reason based on cost, reliability, lead time, material coverage" }
                    }
                },
                candidates = fallbackRecommendations.Select(r => new
                {
                    r.SupplierId,
                    r.Source,
                    r.CompanyName,
                    r.Address,
                    r.WebsiteUrl,
                    r.GoogleMapsUrl,
                    r.Rating,
                    r.ReviewCount,
                    r.DistanceEstimate,
                    r.EstimatedTotalCost,
                    r.AverageLeadTimeDays,
                    r.ReliabilityScore,
                    r.DefectRatePct,
                    r.AvgDeliveryDelay,
                    r.BalancedScore,
                    r.MatchedMaterialCount,
                    r.RequestedMaterialCount,
                    materials = r.Lines.Select(l => new
                    {
                        l.MaterialId,
                        l.MaterialName,
                        l.Quantity,
                        l.UnitPrice,
                        l.EstimatedLineCost,
                        l.LeadTimeDays
                    })
                })
            });

            var aiResponse = await _googleAIClient.GenerateTextAsync(systemInstruction, input);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Text))
                return null;

            var json = ExtractJson(aiResponse.Text);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var parsed = JsonSerializer.Deserialize<AiRankingEnvelope>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed?.Recommendations == null || parsed.Recommendations.Count == 0)
                    return null;

                var byKey = fallbackRecommendations.ToDictionary(r => BuildRecommendationKey(r.SupplierId, r.CompanyName, r.Source), r => r);
                var ranked = new List<SupplierRecommendationResponse>();

                foreach (var item in parsed.Recommendations)
                {
                    var key = BuildRecommendationKey(item.SupplierId, item.CompanyName, item.Source);
                    if (byKey.TryGetValue(key, out var recommendation))
                    {
                        recommendation.Reason = string.IsNullOrWhiteSpace(item.Reason)
                            ? recommendation.Reason
                            : item.Reason;
                        ranked.Add(recommendation);
                    }
                }

                foreach (var fallback in fallbackRecommendations)
                {
                    if (!ranked.Any(r => r.SupplierId == fallback.SupplierId))
                    {
                        ranked.Add(fallback);
                    }
                }

                return new AiRankingResult
                {
                    Summary = parsed.Summary,
                    Recommendations = ranked
                };
            }
            catch
            {
                return null;
            }
        }

        private static string BuildReason(SupplierRecommendationResponse response)
        {
            var coverage = response.MatchedMaterialCount == response.RequestedMaterialCount
                ? "covers all requested materials"
                : $"covers {response.MatchedMaterialCount}/{response.RequestedMaterialCount} requested materials";

            return $"{coverage}; estimated cost {response.EstimatedTotalCost:N0}; reliability {response.ReliabilityScore:N1}/100; average lead time {response.AverageLeadTimeDays:N1} days.";
        }

        private static double NormalizeInverse(double value, double min, double max)
        {
            if (Math.Abs(max - min) < 0.0001) return 100;
            return Clamp(100 - ((value - min) / (max - min) * 100), 0, 100);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string? ExtractJson(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith("```"))
            {
                var firstNewLine = trimmed.IndexOf('\n');
                var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewLine >= 0 && lastFence > firstNewLine)
                {
                    trimmed = trimmed.Substring(firstNewLine + 1, lastFence - firstNewLine - 1).Trim();
                }
            }

            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            return start >= 0 && end > start ? trimmed.Substring(start, end - start + 1) : null;
        }

        private static string BuildRecommendationKey(int supplierId, string? companyName, string? source)
        {
            return $"{supplierId}|{source ?? string.Empty}|{companyName ?? string.Empty}".ToLowerInvariant();
        }

        private static double EstimateWebScore(WebSupplierItem supplier)
        {
            var reliability = EstimateReliabilityScore(supplier);
            var cost = supplier.EstimatedCostLevel?.ToLowerInvariant() switch
            {
                "low" => 90,
                "medium" => 70,
                "high" => 45,
                _ => 60
            };
            return Math.Round((reliability * 0.55) + (cost * 0.25) + ((supplier.MatchedMaterials?.Count ?? 0) * 5), 2);
        }

        private static double EstimateReliabilityScore(WebSupplierItem supplier)
        {
            var ratingScore = supplier.Rating.HasValue ? Clamp((supplier.Rating.Value / 5.0) * 100, 0, 100) : 60;
            var reviewBonus = supplier.ReviewCount.HasValue ? Math.Min(10, supplier.ReviewCount.Value / 50.0) : 0;
            var reliabilityLevel = supplier.ReliabilityLevel?.ToLowerInvariant() switch
            {
                "high" => 90,
                "medium" => 70,
                "low" => 45,
                _ => ratingScore
            };

            return Math.Round(Clamp((reliabilityLevel * 0.65) + (ratingScore * 0.35) + reviewBonus, 0, 100), 2);
        }

        private class SupplierCandidate
        {
            public int SupplierId { get; set; }
            public decimal EstimatedTotalCost { get; set; }
            public double AverageLeadTimeDays { get; set; }
            public double ReliabilityScore { get; set; }
            public double DefectRatePct { get; set; }
            public double AvgDeliveryDelay { get; set; }
            public double BalancedScore { get; set; }
            public int MatchedMaterialCount { get; set; }
            public int RequestedMaterialCount { get; set; }
            public SupplierRecommendationResponse Response { get; set; } = null!;
        }

        private class AiRankingEnvelope
        {
            public string? Summary { get; set; }
            public List<AiRecommendationItem> Recommendations { get; set; } = new List<AiRecommendationItem>();
        }

        private class AiRecommendationItem
        {
            public int SupplierId { get; set; }
            public string? CompanyName { get; set; }
            public string? Source { get; set; }
            public string? Reason { get; set; }
        }

        private class AiRankingResult
        {
            public string? Summary { get; set; }
            public List<SupplierRecommendationResponse> Recommendations { get; set; } = new List<SupplierRecommendationResponse>();
        }

        private class WebSupplierEnvelope
        {
            public string? Summary { get; set; }
            public List<WebSupplierItem> Suppliers { get; set; } = new List<WebSupplierItem>();
        }

        private class WebSupplierItem
        {
            public string? CompanyName { get; set; }
            public string? Address { get; set; }
            public string? ContactPhone { get; set; }
            public string? ContactEmail { get; set; }
            public string? WebsiteUrl { get; set; }
            public string? GoogleMapsUrl { get; set; }
            public double? Rating { get; set; }
            public int? ReviewCount { get; set; }
            public string? DistanceEstimate { get; set; }
            public string? EstimatedCostLevel { get; set; }
            public string? ReliabilityLevel { get; set; }
            public List<string>? MatchedMaterials { get; set; }
            public double? BalancedScore { get; set; }
            public string? Reason { get; set; }
            public List<string>? SourceUrls { get; set; }
        }

        private class AiWebSupplierResult
        {
            public string? Summary { get; set; }
            public List<SupplierRecommendationResponse> Recommendations { get; set; } = new List<SupplierRecommendationResponse>();
        }
    }
}
