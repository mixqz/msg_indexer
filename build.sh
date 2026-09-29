#!/usr/bin/env bash
# Docker 안에서 테스트 + Windows exe 빌드. Mac 본체에는 .NET을 설치하지 않는다.
# 사용: ./build.sh          (테스트 + 빌드)
#       ./build.sh test     (테스트만)
set -euo pipefail
cd "$(dirname "$0")"

IMAGE="mcr.microsoft.com/dotnet/sdk:8.0"
MODE="${1:-all}"

run() {
  docker run --rm \
    -v "$PWD":/src -w /src \
    -v emailindexer-nuget:/root/.nuget/packages \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
    "$IMAGE" bash -c "$1"
}

run "dotnet test tests/EmailIndexer.Core.Tests -c Release --nologo -v q"

if [ "$MODE" != "test" ]; then
  run "dotnet build src/EmailIndexer.App -c Release --nologo -v q -o /src/out/build \
       && mkdir -p /src/dist && cp /src/out/build/EmailIndexer.exe /src/dist/EmailIndexer.exe"
  echo "✅ 결과: $PWD/dist/EmailIndexer.exe ($(du -h dist/EmailIndexer.exe | cut -f1))"
fi
