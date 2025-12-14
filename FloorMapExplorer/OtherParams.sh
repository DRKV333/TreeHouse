#!/bin/bash
thisdir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run $thisdir/../TreeHouse.OtherParams "$@"
#exec dotnet $thisdir/../TreeHouse.OtherParams/bin/Debug/net8.0/TreeHouse.OtherParams.dll "$@"