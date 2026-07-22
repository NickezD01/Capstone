# ==========================================
# GIAI ĐOẠN 1 BUILD (Sử dụng .NET SDK)
# ==========================================
FROM mcr.microsoft.comdotnetsdk8.0 AS build
WORKDIR src

# 1. Copy file .csproj để restore dependencies (tối ưu cache Docker)
# Thay 'BuildSense.csproj' bằng tên file .csproj thực tế của bạn
COPY [BuildSense.csproj, .]
RUN dotnet restore BuildSense.csproj

# 2. Copy toàn bộ mã nguồn và tiến hành Publish
COPY . .
RUN dotnet publish BuildSense.csproj -c Release -o apppublish pUseAppHost=false

# ==========================================
# GIAI ĐOẠN 2 RUNTIME (Chỉ chứa .NET Runtime)
# ==========================================
FROM mcr.microsoft.comdotnetaspnet8.0 AS final
WORKDIR app

# Lấy các file đã build từ giai đoạn 1 sang
COPY --from=build apppublish .

# Render sẽ cấp cổng ngẫu nhiên qua biến $PORT
# Đảm bảo ASP.NET Core lắng nghe cổng này
ENV ASPNETCORE_URLS=http+${PORT}

# Mặc định cổng chạy của .NET 8
EXPOSE 8080

# Chạy ứng dụng DLL
# Thay 'BuildSense.dll' bằng tên file .dll ứng với project của bạn khi build ra
ENTRYPOINT [dotnet, BuildSense.dll]