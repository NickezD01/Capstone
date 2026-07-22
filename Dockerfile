# ==========================================
# GIAI ĐOẠN 1: BUILD
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Copy tất cả các file .csproj vào đúng cấu trúc thư mục tương ứng
COPY ["AI-Integrated Construction Project Management System/cpms_API/cpms_API.csproj", "AI-Integrated Construction Project Management System/cpms_API/"]
COPY ["AI-Integrated Construction Project Management System/cpms_Application/cpms_Application.csproj", "AI-Integrated Construction Project Management System/cpms_Application/"]
COPY ["AI-Integrated Construction Project Management System/cpms_Domain/cpms_Domain.csproj", "AI-Integrated Construction Project Management System/cpms_Domain/"]
COPY ["AI-Integrated Construction Project Management System/cpms_Infrastructure/cpms_Infrastructure.csproj", "AI-Integrated Construction Project Management System/cpms_Infrastructure/"]

# 2. Restore dependencies
RUN dotnet restore "AI-Integrated Construction Project Management System/cpms_API/cpms_API.csproj"

# 3. Copy toàn bộ mã nguồn vào container
COPY . .

# 4. Chuyển vào thư mục chứa project API và Publish
WORKDIR "/src/AI-Integrated Construction Project Management System/cpms_API"
RUN dotnet publish "cpms_API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# GIAI ĐOẠN 2: RUNTIME
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Thiết lập cổng kết nối
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Chạy file dll của project API
ENTRYPOINT ["dotnet", "AntiPhisher.API.dll"]