#!/bin/bash

set -euxo pipefail

if [[ $# -lt 1 ]]; then
    echo "Usage: ConvertMapData.sh /path/to/Otherland"
    exit 1
fi

thisdir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
gamedir=$1

tempdir=$thisdir/temp
mkdir -p $tempdir

paramlist=$tempdir/paramlist.db
if [[ ! -f $paramlist ]]; then 
    $thisdir/OtherParams.sh parse -d $paramlist -l $gamedir/Atlas/data/otherlandgame/content/dbbba21e-2342-4357-a777-302ed11b978b/paramlist.ini
fi

content=$tempdir/content.db
instance=$tempdir/instance.db
if [[ ! -f $content ]] || [[ ! -f $instance ]]; then
    rm -f $content
    rm -f $instance

    cp $gamedir/Atlas/data/otherlandgame/content/dbbba21e-2342-4357-a777-302ed11b978b/content.db $tempdir
    cp $gamedir/Atlas/data/otherlandgame/content/dbbba21e-2342-4357-a777-302ed11b978b/instance.db $tempdir

    $thisdir/OtherParams.sh json-convert -d $paramlist -c $content -i $instance
fi

otherlandupk=$tempdir/Otherland.upk
if [[ ! -f $otherlandupk ]]; then
    $thisdir/decompress.sh $gamedir/UnrealEngine3/AmunGame/CookedPCConsole $tempdir Otherland.upk
fi

datadir=$thisdir/data
mkdir -p $datadir

mapinfo=$datadir/MapInfo.json
if [[ ! -f $mapinfo ]]; then
    $thisdir/MapTiler.sh extract-info -p $otherlandupk -o $mapinfo
fi

geojson=$datadir/GeoJson
if [[ ! -d $geojson ]]; then
    $thisdir/MapTiler.sh extract-geojson -d $instance -i $mapinfo -o $thisdir/data/GeoJson
fi

maps=$tempdir/maps
for p in $gamedir/UnrealEngine3/AmunGame/CookedPCConsole/*FloorMap*; do
    pfile=${p##*/}
    mapdir=$maps/${pfile%_SF.upk}

    if [[ ! -d $mapdir ]]; then
        $thisdir/umodel.sh $gamedir/UnrealEngine3/AmunGame/CookedPCConsole $tempdir/maps $pfile
    fi
done

convmaps=$datadir/maps
for d in $tempdir/maps/*/*; do
    convdir=$convmaps/${d##$tempdir/maps/}

    if [[ ! -d $convdir ]]; then
        $thisdir/MapTiler.sh convert -s $d -t $convdir
    fi
done