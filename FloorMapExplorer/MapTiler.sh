#!/bin/bash
thisdir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run $thisdir/../TreeHouse.MapTiler "$@"
#exec dotnet $thisdir/../TreeHouse.MapTiler/bin/Debug/net8.0/TreeHouse.MapTiler.dll "$@"