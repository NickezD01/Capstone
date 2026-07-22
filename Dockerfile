# ==========================================
# GIAI ĐOẠN 1: BUILD
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file csproj và restore
# LƯU Ý: Thay 'BuildSense.csproj' bằng tên file .csproj thực tế của bạn
COPY ["BuildSense.csproj", "./"]
RUN dotnet restore "BuildSense.csproj"

# Copy mã nguồn và build
COPY . .
RUN dotnet publish "BuildSense.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# GIAI ĐOẠN 2: RUNTIME
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Cấu hình cổng cho Render
ENV ASPNETCORE_URLS=http://+:${PORT}
EXPOSE 8080

ENTRYPOINT ["dotnet", "BuildSense.dll"]