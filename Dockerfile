# ==========================================
# GIAI ĐOẠN BUILD
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy toàn bộ source code vào container
COPY . .

# Publish trực tiếp thông qua đường dẫn csproj của dự án cpms_API
# Lệnh publish sẽ tự động dotnet restore các dependency
RUN dotnet publish "AI-Integrated Construction Project Management System/cpms_API/cpms_API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# GIAI ĐOẠN RUNTIME
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Cấu hình cổng 8080 phù hợp với Render
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# ⚠️ ĐIỂM QUAN TRỌNG: Thay AntiPhisher.API.dll thành cpms_API.dll
ENTRYPOINT ["dotnet", "cpms_API.dll"]